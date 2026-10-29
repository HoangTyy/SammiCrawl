using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json.Nodes;
namespace SammiCrawl.Backend.Controllers;
public sealed record BridgeEvaluationRequest(string Text, JsonObject Profile, List<string>? Images = null);
[ApiController, Authorize, EnableRateLimiting("remote-ai")]
public sealed class AiBridgeController(AiRunner ai, ILogger<AiBridgeController> logger) : ControllerBase
{
    bool Trusted => User.Identity?.AuthenticationType == "RemoteAiKey";
    [HttpGet("/bridge/health")]
    public async Task<IActionResult> Health()
    {
        if (!Trusted) return Unauthorized();
        if (ai.IsRemote) return StatusCode(503, new { error = "Máy gateway cần chạy AI local, không được trỏ vòng về gateway khác." });
        return Ok(await ai.Status());
    }
    [HttpPost("/bridge/rewrite"), RequestSizeLimit(30000000)]
    public async Task<IActionResult> Rewrite(RewriteRequest request)
    {
        if (!Trusted) return Unauthorized();
        if (ai.IsRemote) return StatusCode(503);
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 20000 || (request.Instructions?.Length ?? 0) > 10000 || !double.IsFinite(request.Temperature)) return BadRequest(new { error = "Nội dung/chỉ dẫn không hợp lệ hoặc quá dài." });
        if (AiImages.Validate(request.Images) is string imageError) return BadRequest(new { error = imageError });
        if (!ai.TryAcquire()) { Response.Headers.RetryAfter = "10"; return Conflict(new { error = "AI đang bận; chỉ chạy một model mỗi lần." }); }
        try { return Ok(new { text = await ai.Rewrite(request) }); }
        catch (UnchangedRewriteException ex) { return UnprocessableEntity(new { error = ex.Message }); }
        catch { return StatusCode(502, new { error = "Ollama chưa hoàn tất viết lại. Kiểm tra model trên máy AI." }); }
        finally { ai.Release(); }
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
        if (!ai.TryAcquire()) { Response.Headers.RetryAfter="10"; return Conflict(new { error="AI đang bận; chỉ chạy một model mỗi lần." }); }
        try { return Ok(await ai.Evaluate(request.Text,request.Profile,request.Images??[])); }
        catch (Exception ex)
        {
            var failure = AiGatewayException.FromException(ex);
            logger.LogWarning("Clef gateway failed: {Code}; exception {ExceptionType}; trace {TraceId}", failure.Code, ex.GetType().Name, HttpContext.TraceIdentifier);
            return StatusCode(failure.Status,new { error=failure.Message, code=failure.Code });
        }
        finally { ai.Release(); }
    }
}
