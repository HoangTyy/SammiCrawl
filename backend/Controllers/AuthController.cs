using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Controllers;

[ApiController]
[Authorize]
public sealed class AuthController(IAuthService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("/api/auth/status")]
    public async Task<IActionResult> Status() => (await service.Status(HttpContext)).ToActionResult();

    [AllowAnonymous]
    [HttpPost("/api/auth/setup")]
    public async Task<IActionResult> Setup([FromBody] Credentials r) => (await service.Setup(r, HttpContext)).ToActionResult();

    [AllowAnonymous]
    [HttpPost("/api/auth/login")]
    public async Task<IActionResult> Login([FromBody] Credentials r) => (await service.Login(r, HttpContext)).ToActionResult();

    [HttpPost("/api/auth/logout")]
    public async Task<IActionResult> Logout() => (await service.Logout(HttpContext)).ToActionResult();
}
