using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Controllers;

[ApiController]
[Authorize]
public sealed class SystemController(ISystemService service) : ControllerBase
{
    [HttpGet("/api/health")]
    public async Task<IActionResult> Health() => (await service.Health()).ToActionResult();

    [HttpGet("/api/logs")]
    public async Task<IActionResult> Logs() => (await service.Logs()).ToActionResult();
}
