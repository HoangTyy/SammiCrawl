using Microsoft.AspNetCore.Antiforgery;

namespace SammiCrawl.Backend.Infrastructure;

public static class RequestSecurityExtensions
{
    public static IApplicationBuilder UseProjectSecurityHeaders(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none'; object-src 'none'; base-uri 'self'";
            if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/bridge") || context.Request.Path.StartsWithSegments("/media"))
                context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        await next();
    });

    public static IApplicationBuilder UseProjectAntiforgery(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var unsafeMethod = !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method);
        if (unsafeMethod && context.Request.Path.StartsWithSegments("/api") && context.User.Identity?.AuthenticationType != "WorkerKey")
        {
            try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { error = "Phiên xác nhận đã hết hiệu lực. Tải lại trang rồi thử lại.", code = "CSRF_TOKEN_INVALID" });
                return;
            }
        }
        await next();
    });
}
