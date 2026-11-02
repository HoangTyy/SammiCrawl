using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
namespace SammiCrawl.Backend.Controllers;

[ApiController,Authorize]
public sealed class FacebookShareController(FacebookShareService service):ControllerBase
{
    [HttpPost("/api/articles/{id:guid}/share"),RequestSizeLimit(4096)]
    public async Task<IActionResult> Submit(Guid id,JsonObject request)=>(await service.Submit(id,request,User.Identity?.Name??"")).ToActionResult();
    [HttpPost("/api/share-jobs/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)=>(await service.Cancel(id)).ToActionResult();
}
