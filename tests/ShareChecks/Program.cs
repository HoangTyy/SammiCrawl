using System.Text.Json.Nodes;
using Microsoft.Playwright;
using SammiCrawl.Backend.Services;
using SammiCrawl.Backend.Models;
using SammiCrawl.Backend.Repositories;

var checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);checks++;Console.WriteLine("PASS: "+label);}
var repo=new MemoryRepository();var brandId=Guid.NewGuid();var articleId=Guid.NewGuid();var accountId=Guid.NewGuid();
const string source="https://www.facebook.com/groups/123/posts/456/";
const string target="https://www.facebook.com/groups/789/";
var brand=new JsonObject{["id"]=brandId.ToString(),["channels"]=new JsonArray(new JsonObject{["id"]="group",["name"]="Editable channel name",["type"]="group",["url"]=target,["active"]=true})};
repo.Rows.Add(new(){Id=brandId,Kind="brands",Json=brand.ToJsonString()});
var original=new JsonObject{["id"]=articleId.ToString(),["brandId"]=brandId.ToString(),["sourceUrl"]=source,["sourceAuthor"]="Original Author",["content"]="Original content"};
repo.Rows.Add(new(){Id=articleId,Kind="articles",Json=original.ToJsonString()});
var account=new Document{Id=accountId,Kind="accounts",ProtectedSecret="fixture-not-a-cookie",Json=new JsonObject{["role"]="publish",["enabled"]=true,["facebookStatus"]="active"}.ToJsonString()};repo.Rows.Add(account);
var identity=new CrawlWorkerIdentity(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Crawl:WorkerId"]="share-fixture"}).Build());
var service=new FacebookShareService(repo,identity);
JsonObject Request(bool confirmed=true)=>new(){["channelId"]="group",["publishAccountId"]=accountId.ToString(),["confirmed"]=confirmed};
Check((await service.Submit(articleId,Request(false),"fixture")).Status==400&&repo.Rows.Count==3,"no real share queued without explicit confirmation");
var invalid=Request();invalid["channelId"]="missing";Check((await service.Submit(articleId,invalid,"fixture")).Status==400,"unknown channel rejected");
var accountJson=account.Json;account.Json="{\"role\":\"crawl\",\"enabled\":true,\"facebookStatus\":\"active\"}";
Check((await service.Submit(articleId,Request(),"fixture")).Status==400,"crawl account cannot publish");account.Json=accountJson;
var result=await service.Submit(articleId,Request(),"fixture");var job=(JsonObject)result.Value!;
Check(result.Status==200&&job["status"]?.ToString()=="share_queued"&&job["sourceAuthor"]?.ToString()=="Original Author"&&job["sourceUrl"]?.ToString()==source,"native share preserves source URL and author snapshot");
Check((await service.Submit(articleId,Request(),"fixture")).Status==200&&repo.Rows.Count==4,"duplicate submit keeps one job across accounts/machines");
Check(repo.Rows.Single(d=>d.Id==articleId).Json==original.ToJsonString(),"share does not mutate source");
Check((await service.Cancel(Guid.Parse(job["id"]!.ToString()))).Status==200,"queued share can be cancelled");
Check((await service.Cancel(Guid.Parse(job["id"]!.ToString()))).Status==409,"cancel cannot overwrite a completed or cancelled state");

using var pw=await Playwright.CreateAsync();await using var browser=await pw.Chromium.LaunchAsync(new(){Headless=true});
var adapter=new FacebookShareBrowser();var submits=0;var gateCalls=0;
async Task<FacebookShareResult> Run(string mode)
{
    await using var context=await browser.NewContextAsync();var page=await context.NewPageAsync();page.SetDefaultTimeout(1000);
    await context.RouteAsync("**/*",async route=>{
        if(route.Request.Url.TrimEnd('/')==target.TrimEnd('/')){await route.FulfillAsync(new(){ContentType="text/html",Body="<h1>Actual group</h1>"});return;}
        if(route.Request.Url.TrimEnd('/')!=source.TrimEnd('/'))throw new Exception("Unexpected external request");
        var groupLink=mode=="wrong-target"?"https://www.facebook.com/groups/999/":target;
        var finalSource=mode=="missing-source"?"https://www.facebook.com/groups/123/posts/999/":source;
        var pending=mode=="pending";
        var resultMessage=mode=="unknown"?"":pending?"Your post is pending approval":"Your post has been shared";
        var permalink=mode=="no-link"||mode=="unknown"||pending?"":"<a href=\"https://www.facebook.com/groups/789/posts/1000/\">View post</a>";
        var html="""
        <html><body>
        <button id='share'>Share</button><div id='menu' hidden><button id='choose'>Share to a group</button></div>
        <div id='searchDialog' role='dialog' hidden><input type='search' aria-label='Search groups'><div role='button' id='groupRow'><a id='groupResult'>Actual group</a></div></div>
        <div id='finalDialog' role='dialog' hidden><button disabled>Public group</button><a id='target'>Actual group</a><a id='original'>Original Author</a><button id='send'>Post</button></div>
        <div id='result' role='status'></div>
        <script>
        share.onclick=()=>menu.hidden=false;
        choose.onclick=()=>{menu.hidden=true;searchDialog.hidden=false;};
        groupRow.onclick=e=>{e.preventDefault();searchDialog.hidden=true;finalDialog.hidden=false;};
        send.onclick=()=>{fetch('/fixture-submit',{method:'POST'});finalDialog.hidden=true;result.innerHTML=RESULT;};
        </script></body></html>
        """;
        html=html.Replace("id='groupResult'","id='groupResult' href='"+groupLink+"'").Replace("id='target'","id='target' href='"+target+"'").Replace("id='original'","id='original' href='"+finalSource+"'").Replace("RESULT",System.Text.Json.JsonSerializer.Serialize(resultMessage+permalink));
        await route.FulfillAsync(new(){ContentType="text/html",Body=html});
    });
    await context.RouteAsync("**/fixture-submit",async route=>{submits++;await route.FulfillAsync(new(){Body="ok"});});
    return await adapter.Share(page,source,target,()=>{gateCalls++;return Task.FromResult(mode!="gate-denied");});
}
var success=await Run("success");Check(success.Status=="published"&&success.Url=="https://www.facebook.com/groups/789/posts/1000/"&&submits==1,"offline DOM native share reaches exact group with original author and one click");
Check((await Run("pending")).Status=="share_pending_approval"&&submits==2,"group moderation never counted as published");
Check((await Run("no-link")).Status=="share_submitted"&&submits==3,"confirmation without permalink kept separate from publication");
var before=submits;Check((await Run("wrong-target")).Code=="GROUP_NOT_VERIFIED"&&submits==before,"same-name wrong group cannot receive a post");
Check((await Run("missing-source")).Code=="SHARE_PREVIEW_NOT_VERIFIED"&&submits==before,"missing original attribution blocks submit");
Check((await Run("gate-denied")).Code=="SHARE_CONTEXT_CHANGED"&&submits==before,"last-moment account/lease gate blocks send");
var unknownBefore=submits;Check((await Run("unknown")).Status=="share_unknown"&&submits==unknownBefore+1,"unconfirmed delivery stops after one submit without retry");
original["sourceUrl"]="https://user:secret@www.facebook.com/groups/123/posts/456/";repo.Rows.Single(d=>d.Id==articleId).Json=original.ToJsonString();
Check((await service.Submit(articleId,Request(),"fixture")).Status==400,"source credentials rejected before queue/browser");
original["sourceUrl"]="https://www.facebook.com/photo/?fbid=123";repo.Rows.Single(d=>d.Id==articleId).Json=original.ToJsonString();
Check((await service.Submit(articleId,Request(),"fixture")).Status==400,"unsupported source identifiers cannot collide in queue");
Console.WriteLine($"Share checks passed: {checks}. Offline DOM only, no Facebook/AI/real database.");

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
    public Task<Document> SaveManualPublicationAsync(Document record,string auditMessage){var existing=Rows.FirstOrDefault(d=>d.Id==record.Id&&d.Kind=="drafts");if(existing is not null)return Task.FromResult(existing);Rows.Add(record);return Task.FromResult(record);}
    public Task<bool> TryUpdateDraftAsync(Guid id,string expectedJson,string updatedJson,string auditMessage){var row=Rows.FirstOrDefault(d=>d.Kind=="drafts"&&d.Id==id&&d.Json==expectedJson);if(row is null)return Task.FromResult(false);row.Json=updatedJson;return Task.FromResult(true);}
    public Task<bool> DeleteTaskAsync(Guid id)=>Task.FromResult(Rows.RemoveAll(d=>d.Kind=="tasks"&&d.Id==id)>0);
    public Task<bool> TryUpdateTaskAsync(Guid id,string expectedJson,string updatedJson,string auditMessage){var row=Rows.FirstOrDefault(d=>d.Kind=="tasks"&&d.Id==id&&d.Json==expectedJson);if(row is null)return Task.FromResult(false);row.Json=updatedJson;return Task.FromResult(true);}
    public Task<bool> TryUpdateBrandAsync(Guid id, string expectedJson, string updatedJson, string auditMessage) => throw new NotSupportedException();
}
