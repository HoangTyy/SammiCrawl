using System.Security.Claims;

namespace SammiCrawl.Backend.Infrastructure;

public static class RequestPipelineExtensions
{
    public static WebApplication UseProjectPipeline(this WebApplication app)
    {
        app.Use(async (ctx, next) =>
        {
            try { await next(); }
            catch (Exception ex) { app.Logger.LogError(ex, "Request failed: {Path}", ctx.Request.Path); if (!ctx.Response.HasStarted) { ctx.Response.StatusCode = 500; await ctx.Response.WriteAsJsonAsync(new { error = "Xử lý thất bại. Kiểm tra nhật ký máy chủ." }); } }
        });
        app.UseDefaultFiles(); app.UseStaticFiles();
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/bridge"))
            {
                var expected = app.Configuration["RemoteAiKey"];
                var supplied = ctx.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(expected) || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(supplied), System.Text.Encoding.UTF8.GetBytes("Bearer " + expected))) { ctx.Response.StatusCode = 401; return; }
                ctx.Items["RemoteAi"] = true;
                ctx.Request.Path = ctx.Request.Path.Value switch { "/bridge/rewrite" => "/api/ai/rewrite", "/bridge/health" => "/api/health", _ => "/api/bridge-not-found" };
            }
            await next();
        });
        app.UseRouting(); app.UseAuthentication();
        app.Use(async (ctx, next) =>
        {
            if (ctx.Items.ContainsKey("RemoteAi"))
                ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "RemoteBackend") }, "RemoteAiKey"));
            else if (app.Configuration.GetValue<bool>("LocalDevelopment:SkipLogin")
                && (ctx.Request.Host.Host is "localhost" or "127.0.0.1" or "::1")
                && !ctx.Request.Headers.ContainsKey("CF-Connecting-IP")
                && !ctx.Request.Headers.ContainsKey("Forwarded")
                && !ctx.Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                ctx.Items["LocalMode"] = true;
                ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "LocalDeveloper") }, "LocalDevelopment"));
            }
            await next();
        });
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api/accounts") && ctx.Request.Path.Value?.EndsWith("/runtime-error") == true)
            {
                var key = app.Configuration["WorkerApiKey"]; var supplied = ctx.Request.Headers.Authorization.ToString();
                if (!string.IsNullOrEmpty(key) && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(supplied), System.Text.Encoding.UTF8.GetBytes("Bearer " + key))) ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "TrustedWorker") }, "WorkerKey"));
            }
            await next();
        });
        app.UseAuthorization();

        return app;
    }
}
