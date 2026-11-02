using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using SammiCrawl.Backend.Controllers;
using SammiCrawl.Backend.Contracts;
using SammiCrawl.Backend.Infrastructure;
using SammiCrawl.Backend.Services;

var checks=0;
void Check(bool ok,string message){if(!ok)throw new Exception("FAIL: "+message);checks++;Console.WriteLine("PASS: "+message);}
async Task Until(Func<Task<bool>> condition){var timer=Stopwatch.StartNew();while(timer.Elapsed<TimeSpan.FromSeconds(8)){if(await condition())return;await Task.Delay(10);}throw new Exception("Fixture deadline exceeded");}
var time=new ManualClock();
var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Configuration["RemoteAiKey"]="fixture-queue-key";
builder.Services.AddProjectSecurity();builder.Services.AddControllers().AddApplicationPart(typeof(AiJobsController).Assembly);
var fake=new FixtureAiRunner();builder.Services.AddSingleton<AiRunner>(fake);
builder.Services.AddSingleton<TimeProvider>(time);
builder.Services.Configure<AiQueueOptions>(o=>{o.MaxJobs=3;o.WaitTimeoutSeconds=2;o.ResultTtlSeconds=10;});
builder.Services.AddScoped<IContentService>(_=>new FixtureDraftService(fake));builder.Services.AddSingleton<DraftRewriteQueue>();
builder.Services.AddSingleton<AiGatewayQueue>();builder.Services.AddHostedService(sp=>sp.GetRequiredService<AiGatewayQueue>());
var app=builder.Build();app.UseProjectPipeline();app.MapControllers();await app.StartAsync();
var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var client=new HttpClient{BaseAddress=new Uri(address)};client.DefaultRequestHeaders.Authorization=new("Bearer","fixture-queue-key");
using var anon=new HttpClient{BaseAddress=new Uri(address)};
AiJobRequest Request(string text,Guid? id=null,string operation="rewrite")=>operation=="rewrite"?new(id??Guid.NewGuid(),operation,new RewriteRequest(text)):new(id??Guid.NewGuid(),operation,Evaluation:new BridgeEvaluationRequest(text,new JsonObject()));
async Task<JsonObject> Read(Guid id)=>(await client.GetFromJsonAsync<JsonObject>($"bridge/jobs/{id}"))!;
async Task<JsonObject> Done(Guid id){await Until(async()=> (await Read(id))["state"]?.ToString() is "completed" or "failed" or "cancelled");return await Read(id);}
try
{
    var hold=Request("hold-first");
    using(var bad=await anon.PostAsJsonAsync("bridge/jobs",hold))Check(bad.StatusCode==HttpStatusCode.Unauthorized,"anonymous submission rejected before queue");
    using(var submitted=await client.PostAsJsonAsync("bridge/jobs",hold))Check(submitted.StatusCode==HttpStatusCode.Accepted,"submission returns202 without waiting for inference");
    await Until(()=>Task.FromResult(fake.Calls.Contains("hold-first")));
    Check(!fake.TryAcquire(),"queued inference uses the same gate as direct local AI calls");
    var b=Request("second",operation:"evaluate");var c=Request("cancel-me");
    using(var r=await client.PostAsJsonAsync("bridge/jobs",b))Check(r.StatusCode==HttpStatusCode.Accepted,"second machine joins shared queue instead of409 busy");
    using(var r=await client.PostAsJsonAsync("bridge/jobs",c))Check(r.StatusCode==HttpStatusCode.Accepted,"third request admitted up to capacity");
    Check((await Read(b.Id))["queuePosition"]!.GetValue<int>()==1 && (await Read(c.Id))["queuePosition"]!.GetValue<int>()==2,"waiting positions follow FIFO across evaluate and rewrite");
    using(var r=await client.PostAsJsonAsync("bridge/jobs",b))Check(r.StatusCode==HttpStatusCode.Accepted,"same job ID and payload is idempotent even while queue full");
    using(var r=await client.PostAsJsonAsync("bridge/jobs",Request("different",b.Id)))Check(r.StatusCode==HttpStatusCode.Conflict,"same ID with different payload rejected");
    using(var r=await client.PostAsJsonAsync("bridge/jobs",Request("overflow")))Check(r.StatusCode==HttpStatusCode.TooManyRequests && r.Headers.RetryAfter is not null,"full queue returns429 and Retry-After without inference");
    using(var r=await anon.GetAsync($"bridge/jobs/{b.Id}"))Check(r.StatusCode==HttpStatusCode.Unauthorized,"job results require gateway key");
    using(var r=await client.GetAsync("bridge/jobs/not-a-guid"))Check(r.StatusCode==HttpStatusCode.NotFound,"gateway path boundary rejects malformed job paths");
    using(var r=await client.PostAsJsonAsync("bridge/jobs",Request("bad",operation:"unsupported")))Check(r.StatusCode==HttpStatusCode.BadRequest,"unsupported operation rejected before queue");
    using(var r=await client.PostAsJsonAsync("bridge/jobs",new AiJobRequest(Guid.NewGuid(),"rewrite",new RewriteRequest("bad-image",Images:["broken"]))))Check(r.StatusCode==HttpStatusCode.BadRequest,"invalid image rejected before queue");
    using(var r=await client.DeleteAsync($"bridge/jobs/{hold.Id}"))Check(r.StatusCode==HttpStatusCode.Conflict,"running model cannot be cancelled or release gate early");
    using(var r=await client.DeleteAsync($"bridge/jobs/{c.Id}"))Check(r.IsSuccessStatusCode,"queued request can be cancelled before inference");
    fake.Unblock("hold-first");
    Check((await Done(hold.Id))["state"]?.ToString()=="completed" && (await Done(b.Id))["state"]?.ToString()=="completed","queued jobs complete and retain result for polling");
    Check(fake.Calls.SequenceEqual(new[]{"hold-first","second"}) && fake.MaxActive==1,"cancelled work never executes; mixed model requests never overlap");
    var health=await client.GetFromJsonAsync<JsonObject>("bridge/health");
    Check(health?["queueEnabled"]?.GetValue<bool>()==true && health["queue"]?["running"]?.GetValue<int>()==0,"health advertises queue support and idle counters");
    var failed=Request("fail");await client.PostAsJsonAsync("bridge/jobs",failed);
    Check((await Done(failed.Id))["failure"]?["code"]?.ToString()=="CLEF_INVALID_OUTPUT","worker failure is classified and not a successful result");
    var after=Request("after-failure");await client.PostAsJsonAsync("bridge/jobs",after);
    Check((await Done(after.Id))["state"]?.ToString()=="completed" && fake.MaxActive==1,"failed job releases gate for next request");
    var unchanged=Request("unchanged");await client.PostAsJsonAsync("bridge/jobs",unchanged);
    Check((await Done(unchanged.Id))["failure"]?["code"]?.ToString()=="AI_UNCHANGED_OUTPUT","unchanged rewrite preserves422 classification");
    var held=Request("hold-expiry");await client.PostAsJsonAsync("bridge/jobs",held);await Until(()=>Task.FromResult(fake.Calls.Contains("hold-expiry")));
    var expired=Request("must-not-run");await client.PostAsJsonAsync("bridge/jobs",expired);time.Advance(TimeSpan.FromSeconds(3));
    Check((await Read(expired.Id))["failure"]?["code"]?.ToString()=="AI_QUEUE_WAIT_TIMEOUT","expired pending request fails without running");
    fake.Unblock("hold-expiry");await Done(held.Id);
    Check(!fake.Calls.Contains("must-not-run"),"expired queue item cannot run later");
    time.Advance(TimeSpan.FromSeconds(11));using(var r=await client.GetAsync($"bridge/jobs/{b.Id}"))Check(r.StatusCode==HttpStatusCode.NotFound,"results expire rather than retaining captions indefinitely");

    var legacyWork=client.PostAsJsonAsync("bridge/rewrite",new RewriteRequest("hold-legacy"));
    await Until(()=>Task.FromResult(fake.Calls.Contains("hold-legacy")));
    var afterLegacy=Request("after-legacy",operation:"evaluate");await client.PostAsJsonAsync("bridge/jobs",afterLegacy);
    Check(!legacyWork.IsCompleted && (await Read(afterLegacy.Id))["state"]?.ToString()=="queued","legacy blocking API and new job API share the same FIFO queue");
    fake.Unblock("hold-legacy");using(var legacyResult=await legacyWork)Check(legacyResult.IsSuccessStatusCode,"legacy route returns its completed queued result");
    Check((await Done(afterLegacy.Id))["state"]?.ToString()=="completed"&&fake.MaxActive==1,"legacy model work finishes before the next queued evaluation");
    Check(!(await Read(afterLegacy.Id)).ContainsKey("request") && !(await Read(afterLegacy.Id)).ContainsKey("images"),"job snapshots do not expose submitted image payloads");

    // Memory/backpressure and shutdown use real queue with fixture delegates, not Ollama.
    using var q=new AiGatewayQueue(Options.Create(new AiQueueOptions{MaxJobs=3,MaxPayloadBytes=20}),TimeProvider.System,NullLogger<AiGatewayQueue>.Instance);
    await q.StartAsync(default);var blocker=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var active=Guid.NewGuid();
    q.Enqueue(active,"rewrite","a",11,async()=>{await blocker.Task;return new JsonObject();});
    await Until(()=>Task.FromResult(q.Find(active)?.State=="running"));
    try{q.Enqueue(Guid.NewGuid(),"evaluate","b",11,()=>Task.FromResult(new JsonObject()));Check(false,"payload budget");}catch(AiGatewayException ex){Check(ex.Code=="AI_QUEUE_FULL","payload byte budget bounds waiting image memory");}
    var pending=Guid.NewGuid();q.Enqueue(pending,"evaluate","c",1,()=>Task.FromResult(new JsonObject()));
    var stopped=q.StopAsync(default);Check(q.Find(pending)?.Failure?.Code=="AI_GATEWAY_STOPPED","shutdown fails pending jobs without replay");blocker.SetResult();await stopped;

    var drafts=app.Services.GetRequiredService<DraftRewriteQueue>();
    var commonQueue=app.Services.GetRequiredService<AiGatewayQueue>();
    using(var r=await anon.GetAsync("api/rewrite-jobs"))Check(r.StatusCode==HttpStatusCode.Unauthorized,"rewrite queue requires project login");
    using(var r=await client.GetAsync("api/rewrite-jobs"))Check(r.StatusCode==HttpStatusCode.Unauthorized,"gateway key cannot authorize project rewrite queue");
    var draftController=new RewriteJobsController(drafts);
    Check(draftController.Submit(new(Guid.Empty,Guid.NewGuid(),"channel")) is Microsoft.AspNetCore.Mvc.BadRequestObjectResult,"invalid draft request rejected before enqueue");
    var da=Guid.NewGuid();var db=Guid.NewGuid();var dc=Guid.NewGuid();var article=Guid.NewGuid();
    Check(fake.TryAcquire(),"fixture holds common AI gate before draft submissions");
    drafts.Submit(da,article,"draft-a");await Until(()=>Task.FromResult(commonQueue.Find(da)?.State=="running"));
    drafts.Submit(db,article,"draft-b");drafts.Submit(dc,article,"cancelled-draft");
    Check(commonQueue.Find(db)?.QueuePosition==1 && !fake.Calls.Contains("draft-a"),"drafts queue FIFO while Clef owns model gate");
    drafts.Submit(db,article,"draft-b");Check(drafts.List().Length==3,"same draft request ID does not enqueue a second draft");
    Check(!drafts.Cancel(da) && drafts.Cancel(dc),"draft cancellation only removes queued work without releasing running AI");
    fake.Release();await Until(()=>Task.FromResult(commonQueue.Find(db)?.State=="completed"));
    Check(fake.Calls.Where(c=>c is "draft-a" or "draft-b").SequenceEqual(new[]{"draft-a","draft-b"}) && !fake.Calls.Contains("cancelled-draft") && fake.MaxActive==1,"draft jobs run sequentially with one model and cancelled job never runs");
    using(var r=await client.GetAsync($"bridge/jobs/{da}"))Check(r.StatusCode==HttpStatusCode.NotFound,"gateway job API cannot expose project draft result");
    Check(commonQueue.Find(da)?.Result?["branch"]?.ToString()=="rewrite","completed rewrite job returns saved draft for review");
    var invalid=Guid.NewGuid();var next=Guid.NewGuid();drafts.Submit(invalid,article,"invalid");drafts.Submit(next,article,"draft-after-error");
    await Until(()=>Task.FromResult(commonQueue.Find(next)?.State=="completed"));
    Check(commonQueue.Find(invalid)?.Failure?.Status==400 && commonQueue.Find(next)?.State=="completed","failed draft preserves error and does not stop queue");
    Check(drafts.Find(afterLegacy.Id) is null,"rewrite queue cannot expose unrelated gateway jobs");
    await ClientChecks.Run(Check);
    Console.WriteLine($"AI queue checks passed: {checks}. Fake inference only; no database, Facebook or Ollama.");
}
finally{fake.ReleaseAll();await app.StopAsync();await app.DisposeAsync();}

sealed class FixtureDraftService(FixtureAiRunner ai):IContentService
{
    public async Task<ServiceResult> CreateDraft(Guid id,JsonObject request)
    {
        var channel=request["channelId"]!.ToString();
        if(channel=="invalid")return ServiceResult.BadRequest(new{error="Fixture invalid channel"});
        await ai.AcquireAsync();
        try{return ServiceResult.Ok(new JsonObject{["id"]=Guid.NewGuid().ToString(),["articleId"]=id.ToString(),["branch"]="rewrite",["content"]=await ai.Rewrite(new RewriteRequest(channel))});}
        finally{ai.Release();}
    }
    public Task<ServiceResult> ReadVisualContext(Guid id)=>throw new NotSupportedException();
    public Task<ServiceResult> SaveDraft(Guid id,JsonObject request)=>throw new NotSupportedException();
    public Task<ServiceResult> MarkArticlePublished(Guid id,JsonObject request,string actor)=>throw new NotSupportedException();
    public Task<ServiceResult> MarkPublished(Guid id,JsonObject request,string actor)=>throw new NotSupportedException();
    public Task<ServiceResult> ApproveDraft(Guid id,JsonObject request,string reviewer)=>throw new NotSupportedException();
    public Task<ServiceResult> EvaluateArticle(Guid id)=>throw new NotSupportedException();
    public Task<ServiceResult> Rewrite(RewriteRequest request)=>throw new NotSupportedException();
}
sealed class ManualClock:TimeProvider
{
    DateTimeOffset now=DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow()=>now;
    public void Advance(TimeSpan value)=>now+=value;
}
sealed class FixtureAiRunner():AiRunner(new ConfigurationBuilder().Build())
{
    readonly ConcurrentDictionary<string,TaskCompletionSource> holds=new();
    public readonly ConcurrentQueue<string> Calls=new();
    int active;public int MaxActive;
    public void Unblock(string name)=>holds.GetOrAdd(name,_=>new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
    public void ReleaseAll(){foreach(var hold in holds.Values)hold.TrySetResult();}
    public override Task<object> Status()=>Task.FromResult<object>(new{online=true,busy=false,models=new[]{"clef-flash:latest","bonsai-2:27b"}});
    async Task Run(string text)
    {
        var current=Interlocked.Increment(ref active);MaxActive=Math.Max(MaxActive,current);Calls.Enqueue(text);
        try{if(text.StartsWith("hold"))await holds.GetOrAdd(text,_=>new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;if(text=="fail")throw AiGatewayException.InvalidOutput();if(text=="unchanged")throw new UnchangedRewriteException();}
        finally{Interlocked.Decrement(ref active);}
    }
    public override async Task<string> Rewrite(RewriteRequest request){await Run(request.Text);return "fixture rewritten text";}
    public override async Task<JsonObject> Evaluate(string text,JsonNode profile,List<string> images){await Run(text);return new JsonObject{["status"]="review",["score"]=50};}
}
static class ClientChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        var posts=0;var gets=0;Guid id=default;
        using var handler=new Handler(async request=>
        {
            if(request.Method==HttpMethod.Post){posts++;id=(await request.Content!.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<Guid>();return Reply(202,new JsonObject{["id"]=id.ToString(),["state"]="queued"});}
            gets++;return Reply(200,new JsonObject{["id"]=id.ToString(),["state"]=gets==1?"running":"completed",["result"]=new JsonObject{["text"]="done"}});
        });
        using var client=new HttpClient(handler){BaseAddress=new Uri("http://fixture.invalid/")};
        var output=await AiQueuedClient.PostAsync(client,"bridge/rewrite",new RewriteRequest("source"),pollInterval:TimeSpan.Zero);
        check(output["text"]?.ToString()=="done"&&posts==1&&gets==2,"client submits once and polls queued/running until completed");
        var uncertainPosts=0;Guid uncertain=default;
        using var lost=new Handler(async request=>
        {
            if(request.Method==HttpMethod.Post){uncertainPosts++;uncertain=(await request.Content!.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<Guid>();throw new HttpRequestException("fixture lost submission response");}
            return Reply(200,new JsonObject{["id"]=uncertain.ToString(),["state"]="completed",["result"]=new JsonObject{["text"]="recovered"}});
        });
        using var recovering=new HttpClient(lost){BaseAddress=new Uri("http://fixture.invalid/")};
        var recovered=await AiQueuedClient.PostAsync(recovering,"bridge/rewrite",new RewriteRequest("source"),pollInterval:TimeSpan.Zero);
        check(recovered["text"]?.ToString()=="recovered"&&uncertainPosts==1,"lost submission response recovers by job ID without duplicate POST");
        var missingPosts=0;Guid missing=default;
        using var absent=new Handler(async request=>
        {
            if(request.Method==HttpMethod.Post){missingPosts++;missing=(await request.Content!.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<Guid>();return Reply(202,new JsonObject{["id"]=missing.ToString(),["state"]="queued"});}
            return Reply(404,new JsonObject());
        });
        using var disappeared=new HttpClient(absent){BaseAddress=new Uri("http://fixture.invalid/")};
        try{await AiQueuedClient.PostAsync(disappeared,"bridge/evaluate",new{text="source",profile=new{}},pollInterval:TimeSpan.Zero);check(false,"missing job");}catch(AiGatewayException ex){check(ex.Code=="AI_JOB_NOT_FOUND"&&missingPosts==1,"gateway restart/missing job is explicit failure with no resubmission");}
        var ratePosts=0;
        using var fullHandler=new Handler(request=>{ratePosts++;return Task.FromResult(Reply(429,new JsonObject{["code"]="AI_QUEUE_FULL"}));});
        using var fullClient=new HttpClient(fullHandler){BaseAddress=new Uri("http://fixture.invalid/")};
        try{await AiQueuedClient.PostAsync(fullClient,"bridge/rewrite",new RewriteRequest("source"));check(false,"queue full");}catch(AiGatewayException ex){check(ex.Code=="AI_QUEUE_FULL"&&ratePosts==1,"queue-full submission is not automatically retried");}
        Guid failedId=default;var failedPosts=0;
        using var failedHandler=new Handler(async request=>{if(request.Method==HttpMethod.Post){failedPosts++;failedId=(await request.Content!.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<Guid>();return Reply(202,new JsonObject{["id"]=failedId.ToString(),["state"]="queued"});}return Reply(200,new JsonObject{["id"]=failedId.ToString(),["state"]="failed",["failure"]=new JsonObject{["code"]="AI_UNCHANGED_OUTPUT",["status"]=422}});});
        using var failedClient=new HttpClient(failedHandler){BaseAddress=new Uri("http://fixture.invalid/")};
        try{await AiQueuedClient.PostAsync(failedClient,"bridge/rewrite",new RewriteRequest("source"),pollInterval:TimeSpan.Zero);check(false,"unchanged result");}catch(UnchangedRewriteException){check(failedPosts==1,"queued unchanged rewrite remains422 without resubmission");}
        var legacyPosts=0;
        using var legacyHandler=new Handler(request=>Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/jobs")?Reply(404,new JsonObject()):(++legacyPosts>0?Reply(200,new JsonObject{["text"]="legacy"}):throw new Exception())));
        using var legacy=new HttpClient(legacyHandler){BaseAddress=new Uri("http://fixture.invalid/")};
        check((await AiQueuedClient.PostAsync(legacy,"bridge/rewrite",new RewriteRequest("source")))["text"]?.ToString()=="legacy"&&legacyPosts==1,"older gateway404 falls back to legacy route only once");
    }
    static HttpResponseMessage Reply(int status,JsonObject body)=>new((HttpStatusCode)status){Content=JsonContent.Create(body)};
    sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> handle):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>handle(request);}
}
