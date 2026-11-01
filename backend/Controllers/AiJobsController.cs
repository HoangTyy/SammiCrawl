using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace SammiCrawl.Backend.Controllers;

public sealed record AiJobRequest(Guid Id, string Operation, RewriteRequest? Rewrite = null, BridgeEvaluationRequest? Evaluation = null);

[ApiController, Authorize]
public sealed class AiJobsController(AiRunner ai, AiGatewayQueue queue) : ControllerBase
{
    bool Trusted => User.Identity?.AuthenticationType == "RemoteAiKey";
    [HttpPost("/bridge/jobs"), EnableRateLimiting("remote-ai"), RequestSizeLimit(30000000)]
    public IActionResult Submit(AiJobRequest request)
    {
        if (!Trusted) return Unauthorized();
        if (ai.IsRemote) return StatusCode(503, new { code="AI_GATEWAY_LOOP", error="Gateway phải gọi Ollama local." });
        if (request.Id == Guid.Empty) return BadRequest(new { error="Thiếu mã tác vụ AI." });
        if (request.Operation == "rewrite" && request.Rewrite is { } r && request.Evaluation is null)
        {
            if (string.IsNullOrWhiteSpace(r.Text) || r.Text.Length > 20000 || (r.Instructions?.Length ?? 0) > 10000 || !double.IsFinite(r.Temperature)) return BadRequest(new { error="Nội dung/chỉ dẫn không hợp lệ." });
            if (AiImages.Validate(r.Images) is string error) return BadRequest(new { error });
        }
        else if (request.Operation == "evaluate" && request.Evaluation is { } e && request.Rewrite is null)
        {
            if (string.IsNullOrWhiteSpace(e.Text) || e.Text.Length > 20000 || e.Profile is null || e.Profile.ToJsonString().Length > 50000) return BadRequest(new { error="Nội dung/hồ sơ không hợp lệ." });
            if (AiImages.Validate(e.Images) is string error) return BadRequest(new { error });
        }
        else return BadRequest(new { error="Chỉ hỗ trợ evaluate hoặc rewrite với đúng payload." });
        var payload = JsonSerializer.SerializeToUtf8Bytes(request);
        var hash = Convert.ToHexString(SHA256.HashData(payload));
        try
        {
            var snapshot = queue.Enqueue(request.Id, request.Operation, hash, payload.Length, async () =>
            {
                await ai.AcquireAsync();
                try
                {
                    return request.Operation == "rewrite"
                        ? new JsonObject { ["text"] = await ai.Rewrite(request.Rewrite!) }
                        : await ai.Evaluate(request.Evaluation!.Text, request.Evaluation.Profile, request.Evaluation.Images ?? []);
                }
                finally { ai.Release(); }
            });
            return Accepted($"/bridge/jobs/{snapshot.Id}", snapshot);
        }
        catch (AiGatewayException ex)
        {
            if (ex.Status == 429) Response.Headers.RetryAfter = "10";
            return StatusCode(ex.Status, new { code=ex.Code, error=ex.Message });
        }
    }
    [HttpGet("/bridge/jobs/{id:guid}"), EnableRateLimiting("remote-ai-poll")]
    public IActionResult Status(Guid id)
    {
        if (!Trusted) return Unauthorized();
        if (ai.IsRemote) return StatusCode(503);
        var snapshot = queue.Find(id);
        return snapshot is null ? NotFound(new { code="AI_JOB_NOT_FOUND", error="Tác vụ AI không còn trên gateway (hết hạn hoặc gateway đã khởi động lại). Không tự gửi lại." }) : Ok(snapshot);
    }
    [HttpDelete("/bridge/jobs/{id:guid}"), EnableRateLimiting("remote-ai-poll")]
    public IActionResult Cancel(Guid id)
    {
        if (!Trusted) return Unauthorized();
        if (ai.IsRemote) return StatusCode(503);
        if (queue.Find(id) is null) return NotFound();
        return queue.CancelQueued(id) ? Ok(new { state="cancelled" }) : Conflict(new { code="AI_JOB_NOT_QUEUED", error="Chỉ bỏ được yêu cầu đang chờ. AI đang chạy phải hoàn tất và giải phóng model trước lượt sau." });
    }
}
