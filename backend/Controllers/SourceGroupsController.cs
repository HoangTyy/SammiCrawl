using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SammiCrawl.Backend.Controllers;

[ApiController, Authorize]
public sealed class SourceGroupsController(SourceGroupsService service) : ControllerBase
{
    [HttpGet("/api/source-groups")]
    public async Task<IActionResult> List() => (await service.List()).ToActionResult();
}
