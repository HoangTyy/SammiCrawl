using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SammiCrawl.Backend.Services;

namespace SammiCrawl.Backend.Controllers;

public sealed record DraftRewriteJobRequest(Guid Id, Guid ArticleId, string ChannelId);
[ApiController, Authorize]
[Route("api/rewrite-jobs")]
public sealed class RewriteJobsController(DraftRewriteQueue queue) : ControllerBase
{
    [HttpPost, RequestSizeLimit(4096)]
    public IActionResult Submit(DraftRewriteJobRequest request)
    {
        if (request.Id == Guid.Empty || request.ArticleId == Guid.Empty || string.IsNullOrWhiteSpace(request.ChannelId) || request.ChannelId.Length > 80)
            return BadRequest(new { error = "Chọn bài và kênh đăng hợp lệ trước khi thêm rewrite." });
        try { return Accepted($"/api/rewrite-jobs/{request.Id}", queue.Submit(request.Id, request.ArticleId, request.ChannelId)); }
        catch (AiGatewayException ex) { return StatusCode(ex.Status, new { error=ex.Message, code=ex.Code }); }
    }
    [HttpGet] public IActionResult List() => Ok(queue.List());
    [HttpGet("{id:guid}")] public IActionResult Find(Guid id) => queue.Find(id) is { } job ? Ok(job) : NotFound(new { error="Tác vụ rewrite đã hết hạn hoặc ứng dụng đã khởi động lại. Kiểm tra Bản nháp trước khi gửi lại." });
    [HttpDelete("{id:guid}")]
    public IActionResult Cancel(Guid id) => queue.Find(id) is null ? NotFound() : queue.Cancel(id) ? Ok(new { state="cancelled" }) : Conflict(new { error="Chỉ hủy được rewrite đang chờ; tác vụ đã chạy cần hoàn tất và giải phóng model." });
}
