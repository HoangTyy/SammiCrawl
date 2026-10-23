using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
var provider = builder.Configuration["DatabaseProvider"] ?? "PostgreSql";
builder.Services.AddDbContext<Store>(o =>
{
    var connection = builder.Configuration.GetConnectionString("Database");
    if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)) o.UseNpgsql(connection);
    else o.UseSqlServer(connection);
});
builder.Services.AddDataProtection();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "SammiCrawl.Session"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRecordsService, RecordsService>();
builder.Services.AddScoped<IAccountsService, AccountsService>();
builder.Services.AddScoped<ISystemService, SystemService>();
builder.Services.AddScoped<IWorkflowsService, WorkflowsService>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<IContentReportService, ContentReportService>();
builder.Services.AddScoped<IChannelsService, ChannelsService>();

builder.Services.AddSingleton<AiRunner>();
builder.Services.AddSingleton<FacebookAccountHealth>();
if (args.Contains("--migrate-postgres")) { await DatabaseTransfer.Run(builder.Configuration); return; }
var app = builder.Build();
using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<Store>().Database.EnsureCreatedAsync();
if (args.Contains("--seed-demo"))
{
    using var scope = app.Services.CreateScope();
    await DemoAccountSeeder.Seed(scope.ServiceProvider.GetRequiredService<IProjectRepository>(), app.Configuration);
    return;
}
app.UseProjectPipeline();
app.MapControllers();
app.Map("/api/{**path}", () => Results.NotFound()).WithOrder(int.MaxValue - 1);
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program { }
