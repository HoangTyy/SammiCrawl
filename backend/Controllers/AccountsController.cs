using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Controllers;

[ApiController]
[Authorize]
public sealed class AccountsController(IAccountsService service) : ControllerBase
{
    [HttpPut("/api/records/accounts/{id:guid}")]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] JsonObject data) => (await service.Update(id, data)).ToActionResult();

    [HttpPost("/api/accounts/{id:guid}/check")]
    public async Task<IActionResult> Check([FromRoute] Guid id) => (await service.Check(id)).ToActionResult();

    [HttpPost("/api/accounts/{id:guid}/runtime-error")]
    public async Task<IActionResult> RuntimeError([FromRoute] Guid id, [FromBody] RuntimeAccountError error) => (await service.RuntimeError(id, error, HttpContext)).ToActionResult();
}
