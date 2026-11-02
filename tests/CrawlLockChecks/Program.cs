using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;
using SammiCrawl.Backend.Data;
using SammiCrawl.Backend.Models;
using SammiCrawl.Backend.Repositories;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using Npgsql;
using SammiCrawl.Backend.Services;

var connection=Environment.GetEnvironmentVariable("PG_TEST_CONNECTION")??throw new Exception("Dedicated fixture PostgreSQL required");
var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"DatabaseProvider","PostgreSql"},{"ConnectionStrings:Database",connection}}).Build();
var first=new CrawlGlobalLock(config);var second=new CrawlGlobalLock(config);
if(args.Contains("--probe")){await using var probe=await second.TryAcquire(default);Console.WriteLine(probe is null?"WAIT":"ACQUIRED");return;}
var checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);checks++;Console.WriteLine("PASS: "+label);}
async Task<string> Child()
{
    var start=new ProcessStartInfo("dotnet"){RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);start.ArgumentList.Add("--probe");
    using var child=Process.Start(start)!;var output=await child.StandardOutput.ReadToEndAsync();await child.WaitForExitAsync();if(child.ExitCode!=0)throw new Exception("Fixture child failed");return output.Trim();
}
CrawlGlobalLease? owned=null;
for(var attempt=0;attempt<30&&owned is null;attempt++){try{owned=await first.TryAcquire(default);}catch{await Task.Delay(500);}}
Check(owned is not null,"node A acquires real PostgreSQL session lock");
var lease=owned!;
try
{
    await using var blocked=await second.TryAcquire(default);Check(blocked is null,"independent node B remains waiting while A owns lock");
    Check(await Child()=="WAIT","separate OS process cannot bypass global lock");
    var now=await lease.DatabaseTime(default);Check(Math.Abs((DateTimeOffset.UtcNow-now).TotalSeconds)<10,"heartbeat clock comes from shared database");
    await Task.Delay(2300);Check(!lease.LostToken.IsCancellationRequested,"healthy DB session retains ownership across heartbeat checks");
}
finally{await lease.DisposeAsync();}
Check(await Child()=="ACQUIRED","next process can acquire after previous run releases lock");
await using(var again=await second.TryAcquire(default))Check(again is not null,"nonpooled disposal does not leak advisory lock");
await using(var lost=await first.TryAcquire(default))
{
    Check(lost is not null,"fixture acquires lock before connection-loss test");
    await using var admin=new NpgsqlConnection(connection);await admin.OpenAsync();
    await using var kill=new NpgsqlCommand("SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE application_name=@name",admin);kill.Parameters.AddWithValue("name",lost!.SessionName);await kill.ExecuteNonQueryAsync();
    var timer=Stopwatch.StartNew();while(!lost.LostToken.IsCancellationRequested&&timer.Elapsed<TimeSpan.FromSeconds(10))await Task.Delay(100);
    Check(lost.LostToken.IsCancellationRequested,"terminated DB session cancels linked running worker");
    try{lost.ThrowIfLost();Check(false,"lost ownership");}catch(OperationCanceledException){Check(true,"lost lease cannot continue as a healthy owner");}
    await using var replacement=await second.TryAcquire(default);Check(replacement is not null,"PostgreSQL releases lock when owner connection dies");
}
var clock=DateTimeOffset.UtcNow;
var recent=new JsonObject{["status"]="running",["crawlLockProtocol"]=1,["workerHeartbeatAt"]=clock.ToString("O")};
Check(CrawlOrphanPolicy.Blocks(recent)&&!CrawlOrphanPolicy.Recoverable(recent,clock.AddSeconds(89)),"recent running task blocks next crawl without forced pause");
Check(CrawlOrphanPolicy.Recoverable(recent,clock.AddSeconds(91)),"orphan eligible only after90 seconds with new lock protocol");
recent.Remove("crawlLockProtocol");Check(!CrawlOrphanPolicy.Recoverable(recent,clock.AddDays(1)),"legacy running tasks never silently marked stopped");
var stopping=new JsonObject{["status"]="paused",["pauseAcknowledged"]=false,["crawlLockProtocol"]=1,["workerHeartbeatAt"]=clock.ToString("O")};
Check(CrawlOrphanPolicy.Blocks(stopping)&&!CrawlOrphanPolicy.Recoverable(stopping,clock),"worker still stopping blocks handoff");
stopping["pauseAcknowledged"]=true;Check(!CrawlOrphanPolicy.Blocks(stopping),"acknowledged pause permits next task");
// Two actual workers share a fixture SQL database; fully captured tasks finish without opening Facebook.
var hostBuilder=Host.CreateApplicationBuilder();hostBuilder.Logging.ClearProviders();
hostBuilder.Services.AddDbContext<Store>(options=>options.UseNpgsql(connection));hostBuilder.Services.AddSignalR();hostBuilder.Services.AddSingleton<LiveUpdates>();hostBuilder.Services.AddScoped<IProjectRepository,ProjectRepository>();
using var host=hostBuilder.Build();
using(var scope=host.Services.CreateScope())await scope.ServiceProvider.GetRequiredService<Store>().Database.EnsureCreatedAsync();
var foreign=Guid.NewGuid();
using(var scope=host.Services.CreateScope()){var store=scope.ServiceProvider.GetRequiredService<Store>();store.Documents.Add(new Document{Id=foreign,Kind="tasks",Json=new JsonObject{["id"]=foreign.ToString(),["status"]="running",["workerOwnerId"]="foreign-node",["workerNode"]="Facebook local"}.ToJsonString()});await store.SaveChangesAsync();}
CrawlWorkerIdentity Node(string name)=>new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Crawl:WorkerId",name}}).Build());
using var nodeA=new FacebookCrawlWorker(host.Services.GetRequiredService<IServiceScopeFactory>(),new EphemeralDataProtectionProvider(),new FixtureEnvironment(),NullLogger<FacebookCrawlWorker>.Instance,first,Node("fixture-node-a"));
using var nodeB=new FacebookCrawlWorker(host.Services.GetRequiredService<IServiceScopeFactory>(),new EphemeralDataProtectionProvider(),new FixtureEnvironment(),NullLogger<FacebookCrawlWorker>.Instance,second,Node("fixture-node-b"));
var barrier=await first.TryAcquire(default);Check(barrier is not null,"fixture holds global turn before starting two backend workers");
await nodeA.StartAsync(default);await nodeB.StartAsync(default);await Task.Delay(500);
async Task<string?> State(Guid id){using var scope=host.Services.CreateScope();return JsonNode.Parse((await scope.ServiceProvider.GetRequiredService<Store>().Documents.AsNoTracking().SingleAsync(d=>d.Id==id)).Json)?["status"]?.ToString();}
Check(await State(foreign)=="running","starting backend B does not pause another node's running task");
var ids=new[]{Guid.NewGuid(),Guid.NewGuid()};var tokens=new[]{Guid.NewGuid().ToString(),Guid.NewGuid().ToString()};
using(var scope=host.Services.CreateScope())
{
    var store=scope.ServiceProvider.GetRequiredService<Store>();
    for(var i=0;i<2;i++)
    {
        var articleId=Guid.NewGuid();
        store.Documents.Add(new Document{Id=ids[i],Kind="tasks",Json=new JsonObject{["id"]=ids[i].ToString(),["status"]="pending",["workerOwnerId"]=i==0?"fixture-node-a":"fixture-node-b",["executionToken"]=tokens[i],["url"]="https://facebook.com/fixture",["sourceMode"]="url",["brandId"]=Guid.NewGuid().ToString(),["maxPosts"]=1,["crawledPosts"]=1,["crawlAccountIds"]=new JsonArray()}.ToJsonString()});
        store.Documents.Add(new Document{Id=articleId,Kind="articles",Json=new JsonObject{["id"]=articleId.ToString(),["crawlTaskId"]=ids[i].ToString(),["sourceUrl"]="https://facebook.com/fixture/posts/"+(i+1),["aiStatus"]="recommended",["aiScore"]=60,["aiEvaluatedAt"]=DateTimeOffset.UtcNow.ToString("O")}.ToJsonString()});
    }
    await store.SaveChangesAsync();
}
nodeA.Enqueue(ids[0],tokens[0]);nodeB.Enqueue(ids[1],tokens[1]);await Task.Delay(2300);
Check(await State(ids[0])=="pending"&&await State(ids[1])=="pending","real workers keep both tasks pending while another session owns global turn");
using(var scope=host.Services.CreateScope())await scope.ServiceProvider.GetRequiredService<Store>().Documents.Where(d=>d.Id==foreign).ExecuteDeleteAsync();
// Release barrier once; use a separate local scope because lease disposal is intentionally one-shot.
await barrier!.DisposeAsync();
var peak=0;var deadline=Stopwatch.StartNew();var completed=false;
while(deadline.Elapsed<TimeSpan.FromSeconds(12))
{
    using var scope=host.Services.CreateScope();var rows=await scope.ServiceProvider.GetRequiredService<Store>().Documents.AsNoTracking().Where(d=>ids.Contains(d.Id)).ToListAsync();
    var states=rows.Select(d=>JsonNode.Parse(d.Json)?["status"]?.ToString()).ToArray();peak=Math.Max(peak,states.Count(s=>s=="running"));
    if(states.All(s=>s=="completed")){completed=true;break;}await Task.Delay(10);
}
Check(completed&&peak<=1,"two production workers finish serially under one DB lock without Facebook or AI");
await nodeA.StopAsync(default);await nodeB.StopAsync(default);
var temp=Path.Combine(Path.GetTempPath(),"worker-id-fixture-"+Guid.NewGuid());Directory.CreateDirectory(temp);
try{var identityConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Crawl:WorkerIdentityPath",Path.Combine(temp,"id")}}).Build();Check(new CrawlWorkerIdentity(identityConfig).Id==new CrawlWorkerIdentity(identityConfig).Id,"node identity survives backend recreation on persisted volume");}
finally{Directory.Delete(temp,true);}
Console.WriteLine($"Crawl lock checks passed: {checks}. Dedicated fixture PostgreSQL only; no Facebook, user DB or AI.");

sealed class FixtureEnvironment:IWebHostEnvironment
{
    public string ApplicationName{get;set;}="CrawlLockChecks";public string EnvironmentName{get;set;}="Fixture";
    public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    public string WebRootPath{get;set;}=Path.GetTempPath();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
}
