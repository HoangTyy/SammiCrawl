using Microsoft.AspNetCore.RateLimiting;
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
builder.Services.AddProjectSecurity();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<LiveUpdates>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LiveUpdates>());
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRecordsService, RecordsService>();
builder.Services.AddScoped<IAccountsService, AccountsService>();
builder.Services.AddScoped<FacebookLoginService>();
builder.Services.AddScoped<ArticleIngestService>();
builder.Services.AddScoped<ISystemService, SystemService>();
builder.Services.AddScoped<IWorkflowsService, WorkflowsService>();
builder.Services.AddScoped<TaskControlService>();
builder.Services.AddScoped<SourceGroupsService>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<FacebookShareService>();
builder.Services.AddSingleton<FacebookShareBrowser>();
builder.Services.AddHostedService<FacebookShareWorker>();
builder.Services.AddScoped<IContentReportService, ContentReportService>();
builder.Services.AddScoped<IChannelsService, ChannelsService>();

builder.Services.AddSingleton<CrawlGlobalLock>();
builder.Services.AddSingleton<CrawlWorkerIdentity>();
builder.Services.AddSingleton<FacebookCrawlWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<FacebookCrawlWorker>());
builder.Services.AddSingleton<AiRunner>();
builder.Services.AddOptions<AiQueueOptions>().BindConfiguration("AiQueue");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AiGatewayQueue>();
builder.Services.AddSingleton<DraftRewriteQueue>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AiGatewayQueue>());
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
app.MapHub<LiveUpdatesHub>("/api/live", options => options.CloseOnAuthenticationExpiration = true);
app.Map("/api/{**path}", () => Results.NotFound()).WithOrder(int.MaxValue - 1);
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program { }
