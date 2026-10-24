using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
namespace SammiCrawl.Backend.Controllers;
[ApiController, Authorize]
public sealed class ArticleIngestController(ArticleIngestService service) : ControllerBase
{
    [HttpPost("/api/articles/ingest")]
    public async Task<IActionResult> Ingest([FromBody] JsonObject article) => (await service.Ingest(article)).ToActionResult();
}
