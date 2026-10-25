using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public interface IAuthService
{
    Task<ServiceResult> Status(HttpContext ctx);
    Task<ServiceResult> Setup(Credentials r, HttpContext ctx);
    Task<ServiceResult> Login(Credentials r, HttpContext ctx);
    Task<ServiceResult> Logout(HttpContext ctx);
}

public sealed class AuthService(IProjectRepository db) : IAuthService
{
    public async Task<ServiceResult> Status(HttpContext ctx) { return ServiceResult.Ok(new { setupRequired = !await db.HasUsersAsync(), authenticated = ctx.User.Identity?.IsAuthenticated == true, localMode = false }); }

    static bool Bounded(Credentials r) => r.Email is not null && r.Password is not null && r.Email.Length <= 254 && r.Password.Length <= 1024;
    public async Task<ServiceResult> Setup(Credentials r, HttpContext ctx)
    {
        if (!Bounded(r)) return ServiceResult.BadRequest(new { error = "Email tối đa 254 ký tự; mật khẩu tối đa 1024 ký tự." });
        if (await db.HasUsersAsync()) return ServiceResult.Conflict(new { error = "Dự án đã có tài khoản quản trị." });
        if (r.Email.Length > 254 || !r.Email.Contains('@') || r.Password.Length < 12) return ServiceResult.BadRequest(new { error = "Nhập email hợp lệ và mật khẩu ít nhất 12 ký tự." });
        var u = new ProjectUser { Email = r.Email.Trim().ToLowerInvariant() };
        u.PasswordHash = new PasswordHasher<ProjectUser>().HashPassword(u, r.Password);
        db.AddUser(u); await db.SaveChangesAsync(); await SignIn(ctx, u); return ServiceResult.Ok(new { u.Email });
    }

    public async Task<ServiceResult> Login(Credentials r, HttpContext ctx)
    {
        if (!Bounded(r)) return ServiceResult.BadRequest(new { error = "Email tối đa 254 ký tự; mật khẩu tối đa 1024 ký tự." });
        var u = await db.FindUserByEmailAsync(r.Email.Trim().ToLowerInvariant());
        if (u == null || new PasswordHasher<ProjectUser>().VerifyHashedPassword(u, u.PasswordHash, r.Password) == PasswordVerificationResult.Failed) return ServiceResult.Json(new { error = "Email hoặc mật khẩu không đúng." }, statusCode: 401);
        await SignIn(ctx, u); return ServiceResult.Ok(new { u.Email });
    }

    public async Task<ServiceResult> Logout(HttpContext ctx) { await ctx.SignOutAsync(); return ServiceResult.Ok(); }
    static async Task SignIn(HttpContext ctx, ProjectUser u) => await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()), new Claim(ClaimTypes.Name, u.Email) }, CookieAuthenticationDefaults.AuthenticationScheme)));

}
