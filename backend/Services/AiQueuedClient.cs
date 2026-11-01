using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public static class AiQueuedClient
{
    public static async Task<JsonObject> PostAsync(HttpClient client, string path, object payload, CancellationToken cancellationToken = default,
        TimeSpan? pollInterval = null, TimeSpan? waitTimeout = null)
    {
        var operation = path == "bridge/rewrite" ? "rewrite" : path == "bridge/evaluate" ? "evaluate" : throw new ArgumentException("Unsupported AI operation.");
        var id = Guid.NewGuid();
        var request = new JsonObject { ["id"] = id.ToString(), ["operation"] = operation,
            [operation == "rewrite" ? "rewrite" : "evaluation"] = JsonSerializer.SerializeToNode(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        JsonObject? snapshot = null;
        var submissionUncertain = false;
        // Never replay a submission after a network timeout. The client already knows its job ID.
        try
        {
            using var submitTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            submitTimeout.CancelAfter(TimeSpan.FromSeconds(30));
            using var accepted = await client.PostAsJsonAsync("bridge/jobs", request, submitTimeout.Token);
            if (accepted.StatusCode == HttpStatusCode.NotFound)
                return await AiGatewayClient.PostAsync(client, path, payload); // Older gateway: no new job was accepted.
            if ((int)accepted.StatusCode is 502 or 504 or 520 or 522 or 524) submissionUncertain = true;
            else snapshot = await Read(accepted, cancellationToken);
        }
        catch (HttpRequestException) { submissionUncertain = true; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { submissionUncertain = true; }

        var elapsed = Stopwatch.StartNew();
        var interval = pollInterval ?? TimeSpan.FromSeconds(3);
        var limit = waitTimeout ?? TimeSpan.FromMinutes(45);
        var transientFailures = 0;
        try
        {
            while (elapsed.Elapsed < limit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (snapshot is not null)
                {
                    if (!Guid.TryParse(snapshot["id"]?.ToString(), out var returnedId) || returnedId != id) throw AiGatewayException.InvalidOutput();
                    switch (snapshot["state"]?.ToString())
                    {
                        case "completed":
                            return snapshot["result"] as JsonObject ?? throw AiGatewayException.InvalidOutput();
                        case "failed": case "cancelled":
                            var code = snapshot["failure"]?["code"]?.ToString();
                            var status = snapshot["failure"]?["status"]?.GetValue<int>() ?? 502;
                            if (code == "AI_UNCHANGED_OUTPUT") throw new UnchangedRewriteException();
                            throw AiGatewayException.FromResponse(status, code);
                        case "queued": case "running": break;
                        default: throw AiGatewayException.InvalidOutput();
                    }
                    await Task.Delay(interval, cancellationToken);
                }
                try
                {
                    using var pollTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    pollTimeout.CancelAfter(TimeSpan.FromSeconds(30));
                    using var response = await client.GetAsync($"bridge/jobs/{id}", pollTimeout.Token);
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        throw new AiGatewayException(submissionUncertain ? "AI_SUBMISSION_UNKNOWN" : "AI_JOB_NOT_FOUND",
                            $"Không xác nhận được tác vụ AI {id} (chưa tiếp nhận, hết hạn hoặc gateway khởi động lại). Không tự gửi lại; yêu cầu có thể vẫn đang xử lý.", 503);
                    if ((int)response.StatusCode is 429 or 502 or 503 or 504 or 520 or 522 or 524)
                    {
                        if (++transientFailures > 3) throw new AiGatewayException("AI_POLL_UNAVAILABLE", "Không theo dõi được tác vụ AI. Yêu cầu có thể vẫn đang xử lý; không tự gửi lại.", 503);
                        var retry = response.Headers.RetryAfter?.Delta ?? interval;
                        await Task.Delay(retry > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : retry, cancellationToken);
                        continue;
                    }
                    snapshot = await Read(response, cancellationToken); transientFailures = 0;
                }
                catch (HttpRequestException)
                {
                    if (++transientFailures > 3) throw new AiGatewayException("AI_POLL_UNAVAILABLE", "Mất kết nối theo dõi AI. Yêu cầu có thể vẫn đang xử lý; không tự gửi lại.", 503);
                    await Task.Delay(interval, cancellationToken);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    if (++transientFailures > 3) throw new AiGatewayException("AI_POLL_UNAVAILABLE", "Theo dõi AI quá thời gian. Yêu cầu có thể vẫn đang xử lý; không tự gửi lại.", 503);
                }
            }
            throw new AiGatewayException("AI_WAIT_TIMEOUT", "Đã hết thời gian chờ kết quả AI. Gateway có thể vẫn đang xử lý; không tự gửi lại.", 504);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Best effort: queued work can be removed. Running work must finish/unload before the next job.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try { using var removed = await client.DeleteAsync($"bridge/jobs/{id}", cleanup.Token); } catch { }
            throw;
        }
    }
    static async Task<JsonObject> Read(HttpResponseMessage response, CancellationToken token)
    {
        var body = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
        {
            string? code = null;
            try { code = (JsonNode.Parse(body) as JsonObject)?["code"]?.ToString(); } catch (JsonException) { }
            throw AiGatewayException.FromResponse((int)response.StatusCode, code);
        }
        try { return JsonNode.Parse(body) as JsonObject ?? throw AiGatewayException.InvalidOutput(); }
        catch (JsonException) { throw AiGatewayException.InvalidOutput(); }
    }
}
