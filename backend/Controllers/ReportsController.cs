using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace SammiCrawl.Backend.Controllers;
[ApiController, Authorize]
public sealed class ReportsController(IContentReportService service) : ControllerBase
{
    [HttpGet("/api/reports/content")]
    public async Task<IActionResult> Overview([FromQuery] string? date) => (await service.Overview(date)).ToActionResult();
}
