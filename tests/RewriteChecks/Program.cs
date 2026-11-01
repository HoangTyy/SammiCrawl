using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Logging.Abstractions;
using SammiCrawl.Backend.Contracts;
using SammiCrawl.Backend.Models;
using SammiCrawl.Backend.Repositories;
using SammiCrawl.Backend.Services;

var root = Path.Combine(Path.GetTempPath(), "rewrite-fixture-" + Guid.NewGuid());
Directory.CreateDirectory(Path.Combine(root, "wwwroot", "media"));
var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jD1sAAAAASUVORK5CYII=");
await File.WriteAllBytesAsync(Path.Combine(root, "wwwroot", "media", "fixture.png"), bytes);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root });
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
var gateway = builder.Build();
var calls = new List<JsonObject>();
var answer = "Trường mình ơi, chiếc thẻ sinh viên này khiến ai cũng phải bật cười!";
gateway.MapGet("/bridge/health", () => Results.Json(new { online = true, busy = true, models = new[] { "bonsai-2:27b", "clef-flash:latest" } }));
gateway.MapPost("/bridge/rewrite", (JsonObject body) => { calls.Add(body.DeepClone().AsObject()); return Results.Json(new { text = answer }); });
await gateway.StartAsync();
var url = gateway.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
    ["RemoteAi:BaseUrl"] = url, ["RemoteAi:ApiKey"] = "fixture-only-not-a-real-key"
}).Build();
var ai = new AiRunner(config);
var repo = new MemoryRepository();
var brandId = Guid.NewGuid(); var articleId = Guid.NewGuid();
var article = new JsonObject {
    ["id"] = articleId.ToString(), ["brandId"] = brandId.ToString(), ["content"] = "Ôi trường ơi",
    ["sourceUrl"] = "https://www.facebook.com/groups/123/posts/456/",
    ["media"] = new JsonArray(new JsonObject { ["type"] = "image", ["url"] = "/media/fixture.png" })
};
var articleDoc = new Document { Id = articleId, Kind = "articles", Json = article.ToJsonString() }; repo.Rows.Add(articleDoc);
repo.Rows.Add(new Document { Id = brandId, Kind = "brands", Json = new JsonObject {
    ["name"] = "Fixture", ["channels"] = new JsonArray(new JsonObject { ["id"] = "fixture-channel", ["name"] = "Fixture channel", ["active"] = true, ["tone"] = "Hóm hỉnh" })
}.ToJsonString() });
var historical = new Document { Id = Guid.NewGuid(), Kind = "drafts", Json = "{\"rewriteVisualContext\":\"legacy note\",\"rewriteVisualConfirmedAt\":\"2026-10-28T00:00:00Z\"}" }; repo.Rows.Add(historical);
var historyBefore = historical.Json;
var sourceBefore = articleDoc.Json;
var service = new ContentService(repo, ai, builder.Environment, NullLogger<ContentService>.Instance);
var checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL: " + label); checks++; Console.WriteLine("PASS: " + label); }
JsonObject Request(string branch = "rewrite") => new() { ["branch"] = branch, ["channelId"] = "fixture-channel" };
try
{
    Check(BrandPolicy.Validate(new JsonObject { ["crawlSourceUrls"]="https://www.facebook.com/groups/123/\nhttps://www.facebook.com/stuxpage" }) is null, "brand accepts multiple Page and group crawl sources");
    Check(BrandPolicy.CrawlSources(" https://www.facebook.com/stuxpage \n\nhttps://www.facebook.com/stuxpage").Length == 1, "crawl sources trim blank lines and deduplicate");
    Check(BrandPolicy.ValidateCrawlSources(null) is null && BrandPolicy.ValidateCrawlSources(null,true) is not null, "old brands remain valid but source mode requires configured links");
    foreach (var invalid in new[] { "http://facebook.com/stux", "https://facebook.com.evil.test/stux", "https://facebook.com/groups/1/posts/2/", "https://facebook.com/search/groups/?q=test", "https://user:secret@facebook.com/stux", "https://facebook.com:8443/stux" })
        Check(BrandPolicy.ValidateCrawlSources(JsonValue.Create(invalid)) is not null, "invalid source rejected: " + new Uri(invalid).Host);
    Check(BrandPolicy.ValidateCrawlSources(JsonValue.Create(string.Join("\n",Enumerable.Range(1,31).Select(i=>"https://facebook.com/page"+i)))) is not null, "brand source count bounded at30");

    var crawlAccountId = Guid.NewGuid();
    repo.Rows.Add(new Document { Id=crawlAccountId, Kind="accounts", ProtectedSecret="fixture-unused-session", Json="{\"role\":\"crawl\",\"enabled\":true,\"facebookStatus\":\"active\"}" });
    var brandRow = repo.Rows.Single(r=>r.Id==brandId);
    var brandData=JsonNode.Parse(brandRow.Json)!.AsObject();
    brandData["crawlSourceUrls"]="https://www.facebook.com/groups/123/\nhttps://www.facebook.com/stuxpage"; brandRow.Json=brandData.ToJsonString();
    using var worker = new FacebookCrawlWorker(gateway.Services.GetRequiredService<IServiceScopeFactory>(), new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(), builder.Environment, NullLogger<FacebookCrawlWorker>.Instance);
    var workflows = new WorkflowsService(repo, ai, worker);
    JsonObject CrawlConfig() => new() { ["brandId"]=brandId.ToString(),["sourceMode"]="brand-links",["sourceUrls"]=new JsonArray("https://facebook.com/ignored-client-url"),["crawlAccountIds"]=new JsonArray(crawlAccountId.ToString()),["maxPosts"]=10,["maxScrolls"]=10,["delayMinMs"]=1500,["delayMaxMs"]=4000,["minLikes"]=0,["minComments"]=0,["accountPostLimit"]=100,["accountRestMinMs"]=30000,["accountRestMaxMs"]=50000 };
    var firstCrawl=await workflows.StartCrawl(JsonNode.Parse(CrawlConfig().ToJsonString())!.AsObject());
    var secondCrawl=await workflows.StartCrawl(JsonNode.Parse(CrawlConfig().ToJsonString())!.AsObject());
    Check(firstCrawl.Status==200 && secondCrawl.Status==200 && repo.Rows.Count(r=>r.Kind=="tasks")==2, "multiple crawl submissions queue even while AI busy without starting worker");
    var taskSnapshot=JsonNode.Parse(repo.Rows.First(r=>r.Kind=="tasks").Json)!;
    Check(taskSnapshot["sourceUrls"]!.AsArray().Count==2 && taskSnapshot["sourceUrls"]![0]!.ToString()=="https://www.facebook.com/groups/123/" && !taskSnapshot.ToJsonString().Contains("ignored-client-url"), "crawl snapshots brand sources in order instead of trusting client URLs");
    Check(taskSnapshot["status"]!.ToString()=="pending", "crawl submission persists pending state for UI queue");
    var taskRow=repo.Rows.First(r=>r.Kind=="tasks");var taskId=taskRow.Id;
    var controls=new TaskControlService(repo,worker,ai);
    Check((await controls.Pause(taskId)).Status==200 && JsonNode.Parse(taskRow.Json)?["pauseAcknowledged"]?.GetValue<bool>()==true,"pause queued task acknowledges without starting Facebook");
    var paused=JsonNode.Parse(taskRow.Json)!.AsObject();paused["crawledPosts"]=2;paused["progress"]=20;taskRow.Json=paused.ToJsonString();var oldToken=paused["executionToken"]!.ToString();
    var captured1=new JsonObject{["id"]=Guid.NewGuid().ToString(),["crawlTaskId"]=taskId.ToString(),["sourceAccountId"]=crawlAccountId.ToString(),["sourceGroupUrl"]="https://facebook.com/groups/123/",["sourceUrl"]="https://facebook.com/groups/123/posts/1/",["aiStatus"]="rejected",["aiScore"]=20,["aiEvaluatedAt"]=DateTimeOffset.UtcNow.ToString("O"),["deletedAt"]="fixture"};
    var captured2=captured1.DeepClone().AsObject();captured2["id"]=Guid.NewGuid().ToString();captured2["aiStatus"]="pending";captured2["sourceUrl"]="https://facebook.com/groups/123/posts/2/";
    var checkpoint=new CrawlResumeState(paused,new[]{captured1,captured2});
    Check(checkpoint.Captured==2 && checkpoint.Evaluation.Rejected==1 && checkpoint.PendingEvaluation.Count==1 && checkpoint.ByAccount[crawlAccountId.ToString()]==2 && checkpoint.Seen.Count==2,"resume restores counters, rejected history, account usage and pending Clef");
    Check((await controls.Resume(taskId)).Status==200 && JsonNode.Parse(taskRow.Json)?["crawledPosts"]?.GetValue<int>()==2,"resume queues same task without resetting captured count");
    var resumed=JsonNode.Parse(taskRow.Json)!.AsObject();Check(!CrawlResumeState.CanClaim(resumed,oldToken)&&CrawlResumeState.CanClaim(resumed,resumed["executionToken"]!.ToString()),"old queue token cannot claim resumed task");
    Check((await controls.Resume(taskId)).Status==409,"repeated resume cannot enqueue running or pending task again");
    resumed["status"]="running";taskRow.Json=resumed.ToJsonString();
    Check((await controls.Pause(taskId)).Status==200 && (await controls.Resume(taskId)).Status==409,"active pause waits for worker acknowledgment before resume");
    Check(!await repo.TryUpdateTaskAsync(taskId,resumed.ToJsonString(),resumed.ToJsonString(),"stale"),"stale worker state cannot overwrite pause command");
    var protection=new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider();var records=new RecordsService(repo,protection,new FacebookAccountHealth(protection));
    var keptArticles=repo.Rows.Count(r=>r.Kind=="articles");
    Check((await records.DeleteTask(taskId)).Status==200 && !repo.Rows.Any(r=>r.Kind=="tasks"&&r.Id==taskId) && repo.Rows.Count(r=>r.Kind=="articles")==keptArticles,"delete removes task record completely without deleting articles");
    Check((await controls.Resume(taskId)).Status==404 && (await records.DeleteTask(taskId)).Status==404,"deleted task cannot resume or be deleted twice as success");
    var result = await service.CreateDraft(articleId, Request());
    Check(result.Status == 200 && calls.Count == 1, "image article creates draft with one rewrite call and no OCR confirmation");
    var payload = calls.Single(); var draft = (JsonObject)result.Value!;
    Check(payload["text"]?.ToString() == article["content"]?.ToString() && payload["mode"]?.ToString() == "rewrite", "original caption reaches gateway unchanged, without OCR notes");
    Check(payload["images"]?.AsArray().Count == 1 && payload["images"]?[0]?.ToString() == Convert.ToBase64String(bytes), "actual source image bytes reach the same rewrite request");
    Check(payload["instructions"]?.ToString().Contains("Ảnh đính kèm chỉ giúp hiểu ngữ cảnh") == true && payload["instructions"]?.ToString().Contains("không phiên âm/OCR") == true, "existing gateway receives caption-only output and image-context instructions");
    Check(JsonNode.DeepEquals(draft["media"], article["media"]) && draft["sourceContent"]?.ToString() == "Ôi trường ơi", "draft preserves source caption and original media");
    Check(draft["rewriteInputMode"]?.ToString() == "caption-with-image-context" && draft["rewriteImagesProvided"]?.GetValue<int>() == 1 && draft["rewriteVisualConfirmedAt"] is null && draft["rewriteVisualContext"] is null, "draft records images sent without claiming human confirmation");
    Check(historical.Json == historyBefore && articleDoc.Json == sourceBefore, "source article and historical OCR draft stay unchanged");
    var count = repo.Rows.Count; answer = "Ôi trường ơi";
    Check((await service.CreateDraft(articleId, Request())).Status == 422 && repo.Rows.Count == count, "unchanged AI caption does not save a new draft");
    Check(ai.TryAcquire(), "AI gate released after rejected output"); ai.Release();
    var before = calls.Count; article["media"]![0]!["url"] = "/media/missing.png"; articleDoc.Json = article.ToJsonString();
    Check((await service.CreateDraft(articleId, Request())).Status == 409 && calls.Count == before && repo.Rows.Count == count, "missing image stops rewrite before gateway without saving draft");
    article["media"] = new JsonArray(); articleDoc.Json = article.ToJsonString(); answer = "Trường mình ơi, sao mà thương quá!";
    result = await service.CreateDraft(articleId, Request());
    Check(result.Status == 200 && calls.Last()["images"]?.AsArray().Count == 0, "text-only source still rewrites without OCR");
    before = calls.Count;
    result = await service.CreateDraft(articleId, Request("repost"));
    Check(result.Status == 200 && calls.Count == before && ((JsonObject)result.Value!)["content"]?.ToString() == "Ôi trường ơi", "repost retains original caption without calling AI");
    Console.WriteLine($"Rewrite checks passed: {checks}. No real database, AI, or Facebook used.");
}
finally { await gateway.StopAsync(); await gateway.DisposeAsync(); Directory.Delete(root, recursive: true); }

sealed class MemoryRepository : IProjectRepository
{
    public List<Document> Rows { get; } = [];
    public string? ProviderName => "fixture-memory";
    public Task<Document?> FindDocumentAsync(Guid id, string kind) => Task.FromResult(Rows.FirstOrDefault(d => d.Id == id && d.Kind == kind));
    public Task<Document?> FindJobAsync(Guid id) => FindDocumentAsync(id, "tasks");
    public Task<List<Document>> ListDocumentsAsync(string kind) => Task.FromResult(Rows.Where(d => d.Kind == kind).ToList());
    public void AddDocument(Document document) => Rows.Add(document);
    public void AddLog(Audit audit) { }
    public Task<int> SaveChangesAsync() => Task.FromResult(1);
    public Task ReloadAsync(Document document) => Task.CompletedTask;
    public Task<bool> CanConnectAsync() => Task.FromResult(true);
    public Task<bool> HasUsersAsync() => Task.FromResult(false);
    public Task<ProjectUser?> FindUserByEmailAsync(string email) => Task.FromResult<ProjectUser?>(null);
    public Task<List<Audit>> RecentLogsAsync(int count) => Task.FromResult(new List<Audit>());
    public void AddUser(ProjectUser user) => throw new NotSupportedException();
    public Task<(Document Record, bool Duplicate, string? Reason)> IngestArticleAsync(Guid brandId, JsonObject article) => throw new NotSupportedException();
    public Task<Guid> TrackSourceGroupAsync(Guid brandId, string name, string url, string keyword, Guid accountId, Guid taskId) => throw new NotSupportedException();
    public Task<bool> DeleteTaskAsync(Guid id)=>Task.FromResult(Rows.RemoveAll(d=>d.Kind=="tasks"&&d.Id==id)>0);
    public Task<bool> TryUpdateTaskAsync(Guid id,string expectedJson,string updatedJson,string auditMessage){var row=Rows.FirstOrDefault(d=>d.Kind=="tasks"&&d.Id==id&&d.Json==expectedJson);if(row is null)return Task.FromResult(false);row.Json=updatedJson;return Task.FromResult(true);}
    public Task<bool> TryUpdateBrandAsync(Guid id, string expectedJson, string updatedJson, string auditMessage) => throw new NotSupportedException();
}
