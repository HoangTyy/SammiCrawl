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
        // Port 8081 is the tunnel origin: expose only the authenticated AI gateway.
        app.Use(async (ctx, next) =>
        {
            if (ctx.Connection.LocalPort == 8081 && !ctx.Request.Path.StartsWithSegments("/bridge")) { ctx.Response.StatusCode = 404; return; }
            await next();
        });
        app.UseDefaultFiles(); app.UseStaticFiles();
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/bridge"))
            {
                if (ctx.Request.Path.Value is not ("/bridge/health" or "/bridge/rewrite" or "/bridge/evaluate")) { ctx.Response.StatusCode = 404; return; }
                ctx.Response.Headers.CacheControl = "no-store";
                var expected = app.Configuration["RemoteAiKey"];
                var supplied = ctx.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(expected) || expected.StartsWith("REPLACE_", StringComparison.Ordinal) || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(supplied), System.Text.Encoding.UTF8.GetBytes("Bearer " + expected))) { ctx.Response.StatusCode = 401; return; }
                ctx.Items["RemoteAi"] = true;

            }
            await next();
        });
        app.UseRouting(); app.UseAuthentication();
        app.Use(async (ctx, next) =>
        {
            if (ctx.Items.ContainsKey("RemoteAi"))
                ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "RemoteBackend") }, "RemoteAiKey"));
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
        app.UseRateLimiter();

        return app;
    }
}
