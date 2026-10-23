using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Controllers;

[ApiController]
[Authorize]
public sealed class ContentController(IContentService service) : ControllerBase
{
    [HttpPost("/api/articles/{id:guid}/draft")]
    public async Task<IActionResult> CreateDraft([FromRoute] Guid id, [FromBody] JsonObject request) => (await service.CreateDraft(id, request)).ToActionResult();

    [HttpPost("/api/drafts/{id:guid}/approve")]
    public async Task<IActionResult> ApproveDraft([FromRoute] Guid id, [FromBody] JsonObject request) => (await service.ApproveDraft(id, request, User.Identity?.Name ?? "")).ToActionResult();

    [HttpPut("/api/drafts/{id:guid}")]
    public async Task<IActionResult> SaveDraft([FromRoute] Guid id, [FromBody] JsonObject request) => (await service.SaveDraft(id, request)).ToActionResult();

    [HttpPost("/api/articles/{id:guid}/evaluate")]
    public async Task<IActionResult> EvaluateArticle([FromRoute] Guid id) => (await service.EvaluateArticle(id)).ToActionResult();

    [HttpPost("/api/ai/rewrite")]
    public async Task<IActionResult> Rewrite([FromBody] RewriteRequest r) => (await service.Rewrite(r)).ToActionResult();
}
