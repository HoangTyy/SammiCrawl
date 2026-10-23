using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
namespace SammiCrawl.Backend.Controllers;
[ApiController, Authorize]
public sealed class ChannelsController(IChannelsService service) : ControllerBase
{
    [HttpGet("/api/channels")]
    public async Task<IActionResult> List() => (await service.List()).ToActionResult();
    [HttpPost("/api/brands/{brandId:guid}/channels")]
    public async Task<IActionResult> Create(Guid brandId, [FromBody] JsonObject request) => (await service.Create(brandId, request)).ToActionResult();
    [HttpPut("/api/brands/{brandId:guid}/channels/{channelId}")]
    public async Task<IActionResult> Update(Guid brandId, string channelId, [FromBody] JsonObject request) => (await service.Update(brandId, channelId, request)).ToActionResult();
}
