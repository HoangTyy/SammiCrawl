using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
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
    [RequestSizeLimit(16384)]
    [EnableRateLimiting("auth")]
    [HttpPost("/api/auth/setup")]
    public async Task<IActionResult> Setup([FromBody] Credentials r) => (await service.Setup(r, HttpContext)).ToActionResult();

    [AllowAnonymous]
    [RequestSizeLimit(16384)]
    [EnableRateLimiting("auth")]
    [HttpPost("/api/auth/login")]
    public async Task<IActionResult> Login([FromBody] Credentials r) => (await service.Login(r, HttpContext)).ToActionResult();

    [AllowAnonymous]
    [HttpGet("/api/auth/csrf")]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery) => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [HttpPost("/api/auth/logout")]
    public async Task<IActionResult> Logout() => (await service.Logout(HttpContext)).ToActionResult();
}
