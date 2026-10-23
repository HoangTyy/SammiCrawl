using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Controllers;

[ApiController]
[Authorize]
public sealed class WorkflowsController(IWorkflowsService service) : ControllerBase
{
    [HttpPost("/api/crawl/start")]
    public async Task<IActionResult> StartCrawl([FromBody] JsonObject config) => (await service.StartCrawl(config)).ToActionResult();

    [HttpPost("/api/workflows/{id:guid}/preflight")]
    public async Task<IActionResult> Preflight([FromRoute] Guid id) => (await service.Preflight(id)).ToActionResult();

    [HttpPost("/api/workflows/{id:guid}/runs")]
    public async Task<IActionResult> CreateRun([FromRoute] Guid id) => (await service.CreateRun(id)).ToActionResult();
}
