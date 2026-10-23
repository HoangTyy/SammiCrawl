using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Controllers;

[ApiController]
[Authorize]
public sealed class RecordsController(IRecordsService service) : ControllerBase
{
    [HttpGet("/api/records/{kind}")]
    public async Task<IActionResult> List([FromRoute] string kind, [FromQuery] bool? trash) => (await service.List(kind, trash)).ToActionResult();

    [HttpPost("/api/records/{kind}")]
    public async Task<IActionResult> Create([FromRoute] string kind, [FromBody] JsonObject data) => (await service.Create(kind, data)).ToActionResult();

    [HttpPut("/api/records/{kind}/{id:guid}")]
    public async Task<IActionResult> Update([FromRoute] string kind, [FromRoute] Guid id, [FromBody] JsonObject data) => (await service.Update(kind, id, data)).ToActionResult();

    [HttpDelete("/api/records/{kind}/{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] string kind, [FromRoute] Guid id) => (await service.Delete(kind, id)).ToActionResult();

    [HttpPost("/api/records/{kind}/{id:guid}/restore")]
    public async Task<IActionResult> Restore([FromRoute] string kind, [FromRoute] Guid id) => (await service.Restore(kind, id)).ToActionResult();

    [HttpDelete("/api/records/tasks/{id:guid}")]
    public async Task<IActionResult> CancelTask([FromRoute] Guid id) => (await service.CancelTask(id)).ToActionResult();
}
