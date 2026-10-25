global using SammiCrawl.Backend.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.FileProviders;
using SammiCrawl.Backend.Infrastructure;

var temp = Path.Combine(Path.GetTempPath(), "sammicrawl-security-" + Guid.NewGuid());
var webroot = Path.Combine(temp, "wwwroot");
Directory.CreateDirectory(Path.Combine(webroot, "media"));
Directory.CreateDirectory(Path.Combine(webroot, "assets"));
await File.WriteAllTextAsync(Path.Combine(webroot, "public.txt"), "PUBLIC-FIXTURE");
await File.WriteAllTextAsync(Path.Combine(webroot, "index.html"), "PUBLIC-SHELL");
await File.WriteAllTextAsync(Path.Combine(webroot, "media", "private.txt"), "PRIVATE-FIXTURE");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = temp, WebRootPath = webroot, EnvironmentName = "Development" });
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0", "http://127.0.0.1:8081");
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["RemoteAiKey"] = "fixture-only-bridge-key", ["WorkerApiKey"] = "fixture-only-worker-key" });
builder.Services.AddProjectSecurity();
builder.Services.AddControllers();
builder.Services.AddSingleton<IAuthService, FixtureAuthService>();
var app = builder.Build();
var writes = 0;
app.UseProjectPipeline();
app.MapControllers();
app.MapPost("/api/fixture-login", async (HttpContext ctx) => { await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "fixture-id"), new Claim(ClaimTypes.Name, "fixture-user") }, CookieAuthenticationDefaults.AuthenticationScheme))); return Results.Ok(); }).AllowAnonymous();
app.MapMethods("/api/write", ["POST", "DELETE"], () => { writes++; return Results.Ok(); }).RequireAuthorization();
app.MapGet("/api/fixture-too-large", () => Task.FromException<IResult>(new BadHttpRequestException("fixture-limit", 413))).AllowAnonymous();
app.MapGet("/api/private", () => Results.Ok()).RequireAuthorization();
app.MapGet("/bridge/health", (HttpContext ctx) => Results.Text(ctx.User.Identity?.AuthenticationType ?? "none")).RequireAuthorization();
app.MapPost("/api/accounts/{id:guid}/runtime-error", (HttpContext ctx) => Results.Text(ctx.User.Identity?.AuthenticationType ?? "none")).RequireAuthorization();
app.MapFallbackToFile("index.html");
await app.StartAsync();
var port = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First(a => !a.EndsWith(":8081"));
using var anon = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new Uri(port) };
using var user = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new Uri(port) };
using var gateway = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:8081") };
var checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception("FAIL: " + label); checks++; Console.WriteLine("PASS: " + label); }
async Task<string> Token(HttpClient client) => (await client.GetFromJsonAsync<TokenResponse>("/api/auth/csrf"))!.token;
async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string? token = null, string? bearer = null)
{
    var request = new HttpRequestMessage(method, path);
    if (path == "/api/auth/login") request.Content = JsonContent.Create(new Credentials("fixture@example.invalid", "wrong-fixture-password"));
    if (token is not null) request.Headers.Add("X-CSRF-TOKEN", token);
    if (bearer is not null) request.Headers.Authorization = new("Bearer", bearer);
    return await client.SendAsync(request);
}
try
{
    Check((await anon.GetAsync("/api/fixture-too-large")).StatusCode == HttpStatusCode.RequestEntityTooLarge, "HTTP body limit status413 is not masked as500");
    Check(!FacebookMediaDownload.Allowed(new Uri("http://www.facebook.com/file")) && !FacebookMediaDownload.Allowed(new Uri("https://www.facebook.com.evil.invalid/file")) && !FacebookMediaDownload.Allowed(new Uri("https://user@www.facebook.com/file")) && !FacebookMediaDownload.Allowed(new Uri("https://www.facebook.com:444/file")), "media URL rejects nonHTTPS, spoofed host, userinfo and non443 port");
    using (var source = new GeneratedStream(10000))
    using (var target = new MemoryStream())
    {
        Check(await FacebookMediaDownload.CopyBounded(source, target, 64, CancellationToken.None) is null && source.ReadBytes == 65 && target.Length <= 64, "oversized stream stops after limit plus1 byte");
    }
    var downloadFolder = Path.Combine(temp, "downloads");
    using (var client = new HttpClient(new MediaFixtureHandler(false)))
    {
        var result = await FacebookMediaDownload.Download(client, new Uri("https://fixture.fbcdn.net/file"), downloadFolder, "image", "", "", CancellationToken.None, 64);
        Check(result is null && !Directory.EnumerateFiles(downloadFolder).Any(), "chunked oversized download leaves no partial file");
    }
    using (var client = new HttpClient(new MediaFixtureHandler(true)))
    {
        var result = await FacebookMediaDownload.Download(client, new Uri("https://fixture.fbcdn.net/file"), downloadFolder, "image", "", "", CancellationToken.None, 64);
        Check(result is not null && result.Value.Bytes == 8 && new FileInfo(Path.Combine(downloadFolder, result.Value.FileName)).Length == 8, "bounded download saves supported media atomically");
    }
    var publicFile = await anon.GetAsync("/public.txt");
    Check(publicFile.StatusCode == HttpStatusCode.OK && await publicFile.Content.ReadAsStringAsync() == "PUBLIC-FIXTURE", "public assets remain available");
    Check(publicFile.Headers.GetValues("X-Content-Type-Options").Single() == "nosniff" && publicFile.Headers.GetValues("Content-Security-Policy").Single().Contains("frame-ancestors 'none'"), "security headers on static assets");
    foreach (var method in new[] { HttpMethod.Get, HttpMethod.Head })
    {
        var response = await Send(anon, method, "/media/private.txt");
        Check(response.StatusCode == HttpStatusCode.Unauthorized && response.Headers.CacheControl?.NoStore == true, "anonymous media " + method + " blocked and non-cacheable");
    }
    Check((await anon.GetAsync("/api/private")).StatusCode == HttpStatusCode.Unauthorized, "private API requires login");
    using (var files = new PhysicalFileProvider(webroot))
    {
        var guarded = new PublicAssetsFileProvider(files, Path.Combine(webroot, "media"));
        Check(!guarded.GetFileInfo("media/private.txt").Exists && !guarded.GetFileInfo("assets/../media/private.txt").Exists, "public provider cannot expose canonical media path");
        Check(guarded.GetFileInfo("public.txt").Exists, "public provider permits public assets");
    }
    Check((await Send(user, HttpMethod.Post, "/api/fixture-login")).StatusCode == HttpStatusCode.Forbidden, "login missing CSRF rejected");
    var anonymousToken = await Token(user);
    var login = await Send(user, HttpMethod.Post, "/api/fixture-login", anonymousToken);
    Check(login.StatusCode == HttpStatusCode.OK && login.Headers.GetValues("Set-Cookie").Any(v => v.Contains("SammiCrawl.Session=") && v.Contains("httponly", StringComparison.OrdinalIgnoreCase) && v.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase)), "valid CSRF login issues HttpOnly Strict session");
    Check((await Send(user, HttpMethod.Post, "/api/write", anonymousToken)).StatusCode == HttpStatusCode.Forbidden && writes == 0, "anonymous token invalid after login, no side effect");
    var userToken = await Token(user);
    Check((await Send(user, HttpMethod.Post, "/api/write")).StatusCode == HttpStatusCode.Forbidden && writes == 0, "authenticated POST without token blocked before handler");
    Check((await Send(user, HttpMethod.Delete, "/api/write")).StatusCode == HttpStatusCode.Forbidden && writes == 0, "bodyless DELETE without token blocked");
    var otherToken = await Token(anon);
    Check((await Send(user, HttpMethod.Post, "/api/write", otherToken)).StatusCode == HttpStatusCode.Forbidden && writes == 0, "token from different cookie jar rejected");
    Check((await Send(user, HttpMethod.Post, "/api/write", userToken)).StatusCode == HttpStatusCode.OK && writes == 1, "valid authenticated mutation accepted once");
    Check((await Send(user, HttpMethod.Delete, "/api/write", userToken)).StatusCode == HttpStatusCode.OK && writes == 2, "valid bodyless DELETE accepted");
    var privateFile = await user.GetAsync("/media/private.txt");
    Check(privateFile.StatusCode == HttpStatusCode.OK && privateFile.Headers.CacheControl?.NoStore == true && await privateFile.Content.ReadAsStringAsync() == "PRIVATE-FIXTURE", "authenticated media served with no-store");
    var rangeRequest = new HttpRequestMessage(HttpMethod.Get, "/media/private.txt"); rangeRequest.Headers.Range = new(0, 2);
    Check((await user.SendAsync(rangeRequest)).StatusCode == HttpStatusCode.PartialContent, "authenticated media range requests preserved");
    Check((await user.GetAsync("/media/missing.jpg")).StatusCode == HttpStatusCode.NotFound, "missing private media returns404 instead of app shell");
    Check((await Send(anon, HttpMethod.Get, "/media/private.txt", bearer: "fixture-only-bridge-key")).StatusCode == HttpStatusCode.Unauthorized, "bridge bearer does not grant media access");
    foreach (var path in new[] { "/assets/../media/private.txt", "/assets/%2e%2e/media/private.txt", "/media%2fprivate.txt" })
    {
        var response = await anon.GetAsync(path); Check(!((await response.Content.ReadAsStringAsync()).Contains("PRIVATE-FIXTURE")), "alternate path cannot leak media: " + path);
    }
    Check((await gateway.GetAsync("/api/auth/csrf")).StatusCode == HttpStatusCode.NotFound, "gateway port denies UI/API routes");
    Check((await gateway.GetAsync("/bridge/health")).StatusCode == HttpStatusCode.Unauthorized, "gateway denies missing bearer");
    Check((await Send(gateway, HttpMethod.Get, "/bridge/health", bearer: "fixture-only-bridge-key")).StatusCode == HttpStatusCode.OK, "gateway bearer still works");
    var errorPath = "/api/accounts/00000000-0000-0000-0000-000000000001/runtime-error";
    Check((await Send(anon, HttpMethod.Post, errorPath, bearer: "fixture-only-worker-key")).StatusCode == HttpStatusCode.OK, "trusted worker does not need cookie CSRF");
    Check((await Send(anon, HttpMethod.Post, "/api/write", bearer: "fixture-only-worker-key")).StatusCode == HttpStatusCode.Unauthorized, "worker key cannot authorize unrelated routes");
    var tooLarge = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new Credentials("fixture@example.invalid", new string('x', 17000))) };
    tooLarge.Headers.Add("X-CSRF-TOKEN", otherToken);
    Check((await anon.SendAsync(tooLarge)).StatusCode == HttpStatusCode.RequestEntityTooLarge && FixtureAuthService.LoginCalls == 0, "real AuthController body limit blocks before auth service");
    for (var i = 0; i < 9; i++) Check((await Send(anon, HttpMethod.Post, "/api/auth/login", otherToken)).StatusCode == HttpStatusCode.Unauthorized, "fixture failed-login attempt " + (i + 1));
    var limited = await Send(anon, HttpMethod.Post, "/api/auth/login", otherToken);
    Check(limited.StatusCode == HttpStatusCode.TooManyRequests && limited.Headers.RetryAfter is not null, "login throttling returns429 and Retry-After");
    Console.WriteLine($"Security checks passed: {checks}. No database, Facebook, or Ollama used.");
}
finally { await app.StopAsync(); await app.DisposeAsync(); Directory.Delete(temp, true); }
record TokenResponse(string token);

public interface IAuthService
{
    Task<ServiceResult> Status(HttpContext context);
    Task<ServiceResult> Setup(Credentials credentials, HttpContext context);
    Task<ServiceResult> Login(Credentials credentials, HttpContext context);
    Task<ServiceResult> Logout(HttpContext context);
}
public sealed class FixtureAuthService : IAuthService
{
    public static int LoginCalls;
    public Task<ServiceResult> Status(HttpContext context) => Task.FromResult(ServiceResult.Ok());
    public Task<ServiceResult> Setup(Credentials credentials, HttpContext context) => Task.FromResult(ServiceResult.Conflict());
    public Task<ServiceResult> Login(Credentials credentials, HttpContext context) { LoginCalls++; return Task.FromResult(ServiceResult.Unauthorized()); }
    public Task<ServiceResult> Logout(HttpContext context) => Task.FromResult(ServiceResult.Ok());
}

public sealed class MediaFixtureHandler(bool small) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var content = new StreamContent(new GeneratedStream(small ? 8 : 10000));
        content.Headers.ContentType = new("image/jpeg");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request });
    }
}
public sealed class GeneratedStream(long length) : Stream
{
    public long ReadBytes { get; private set; }
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => ReadBytes; set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) { var size = (int)Math.Min(count, length - ReadBytes); Array.Fill(buffer, (byte)42, offset, size); ReadBytes += size; return size; }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); var size = (int)Math.Min(buffer.Length, length - ReadBytes); buffer.Span[..size].Fill(42); ReadBytes += size; return ValueTask.FromResult(size); }
    public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
