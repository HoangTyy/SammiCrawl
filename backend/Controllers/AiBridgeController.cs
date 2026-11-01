using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json.Nodes;
namespace SammiCrawl.Backend.Controllers;
public sealed record BridgeEvaluationRequest(string Text, JsonObject Profile, List<string>? Images = null);
[ApiController, Authorize, EnableRateLimiting("remote-ai")]
public sealed class AiBridgeController(AiRunner ai, AiGatewayQueue queue, ILogger<AiBridgeController> logger) : ControllerBase
{
    bool Trusted => User.Identity?.AuthenticationType == "RemoteAiKey";
    [HttpGet("/bridge/health")]
    public async Task<IActionResult> Health()
    {
        if (!Trusted) return Unauthorized();
        if (ai.IsRemote) return StatusCode(503, new { error = "Máy gateway cần chạy AI local, không được trỏ vòng về gateway khác." });
        var status = System.Text.Json.JsonSerializer.SerializeToNode(await ai.Status())!.AsObject();
        status["queueEnabled"] = true;
        status["queueVersion"] = 1;
        status["queue"] = System.Text.Json.JsonSerializer.SerializeToNode(queue.Status());
        return Ok(status);
    }
    [HttpPost("/bridge/rewrite"), RequestSizeLimit(30000000)]
    public async Task<IActionResult> Rewrite(RewriteRequest request)
    {
        if (!Trusted) return Unauthorized();
        if (ai.IsRemote) return StatusCode(503);
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 20000 || (request.Instructions?.Length ?? 0) > 10000 || !double.IsFinite(request.Temperature)) return BadRequest(new { error = "Nội dung/chỉ dẫn không hợp lệ hoặc quá dài." });
        if (AiImages.Validate(request.Images) is string imageError) return BadRequest(new { error = imageError });
        return await RunQueued("rewrite", request, async () => new JsonObject { ["text"] = await ai.Rewrite(request) });
    }
    [HttpPost("/bridge/evaluate"), RequestSizeLimit(30000000)]
    public async Task<IActionResult> Evaluate(BridgeEvaluationRequest request)
    {
        if (!Trusted) return Unauthorized();
        if (ai.IsRemote) return StatusCode(503);
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 20000 || request.Profile is null || request.Profile.ToJsonString().Length > 50000 || (request.Images?.Count ?? 0) > 4) return BadRequest(new { error = "Nội dung/hồ sơ quá dài hoặc quá 4 ảnh." });
        foreach (var image in request.Images ?? [])
        {
            if (string.IsNullOrWhiteSpace(image) || image.Length > 7000000) return BadRequest(new { error = "Mỗi ảnh tối đa 5 MB." });
            try { var bytes=Convert.FromBase64String(image); if(bytes.Length<12||bytes.Length>5000000||!((bytes[0]==255&&bytes[1]==216)||(bytes[0]==137&&bytes[1]==80&&bytes[2]==78&&bytes[3]==71)||(System.Text.Encoding.ASCII.GetString(bytes,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(bytes,8,4)=="WEBP")))return BadRequest(new { error="Ảnh phải là JPEG/PNG/WebP base64 hợp lệ." }); }
            catch { return BadRequest(new { error = "Ảnh base64 không hợp lệ." }); }
        }
        return await RunQueued("evaluate", request, () => ai.Evaluate(request.Text,request.Profile,request.Images??[]));
    }
    async Task<IActionResult> RunQueued(string operation, object payload, Func<Task<JsonObject>> work)
    {
        Guid? jobId = null;
        try
        {
            var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload);
            var snapshot = queue.Enqueue(Guid.NewGuid(), operation, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), bytes.Length, async () =>
            {
                await ai.AcquireAsync();
                try { return await work(); } finally { ai.Release(); }
            });
            jobId = snapshot.Id;
            Response.Headers["X-AI-Job-Id"] = snapshot.Id.ToString();
            while (snapshot.State is "queued" or "running")
            {
                await Task.Delay(250, HttpContext.RequestAborted);
                snapshot = queue.Find(snapshot.Id) ?? throw new AiGatewayException("AI_JOB_NOT_FOUND", "Gateway không còn kết quả tác vụ.", 503);
            }
            return snapshot.State == "completed" ? Ok(snapshot.Result)
                : StatusCode(snapshot.Failure?.Status ?? 502, new { error=snapshot.Failure?.Error, code=snapshot.Failure?.Code });
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            if (jobId.HasValue) queue.CancelQueued(jobId.Value);
            throw;
        }
        catch (AiGatewayException ex)
        {
            if (ex.Status == 429) Response.Headers.RetryAfter = "10";
            logger.LogWarning("AI gateway request failed: {Code}; trace {TraceId}", ex.Code, HttpContext.TraceIdentifier);
            return StatusCode(ex.Status, new { code=ex.Code, error=ex.Message });
        }
    }
}
