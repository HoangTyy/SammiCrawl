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
var evaluations=new List<JsonObject>();
var answer = "Trường mình ơi, chiếc thẻ sinh viên này khiến ai cũng phải bật cười!";
gateway.MapGet("/bridge/health", () => Results.Json(new { online = true, busy = true, models = new[] { "bonsai-2:27b", "clef-flash:latest" } }));
gateway.MapPost("/bridge/rewrite", (JsonObject body) => { calls.Add(body.DeepClone().AsObject()); return Results.Json(new { text = answer }); });
gateway.MapPost("/bridge/evaluate",(JsonObject body)=>{evaluations.Add(body.DeepClone().AsObject());return Results.Json(new JsonObject{["status"]="recommended",["score"]=90,["reason"]="Phù hợp chủ đề mẫu",["topic"]="Chủ đề mẫu",["decision"]=new JsonObject{["language"]=new JsonObject{["choice"]="vi"},["topic_relation"]=new JsonObject{["choice"]="direct"}},["warnings"]=new JsonArray()});});
var hermesRequests=new List<JsonObject>();
var hermesFixture=new JsonObject{["summary"]="Nguồn công nghệ sinh học không phù hợp; ưu tiên review sách.",["keywords"]=new JsonArray("review sách","đồ dùng học tập"),["suggestedTopics"]="Review sách",["suggestedRules"]="Không bán hàng"};
gateway.MapPost("/analyze",(JsonObject request)=>{hermesRequests.Add(request);return Results.Json(new{text=hermesFixture.ToJsonString()});});
var systemRequests=new List<JsonObject>();
var planningRequests=new List<JsonObject>();
gateway.MapGet("/api/tags",()=>Results.Json(new { models=new[]{new{name="bonsai-2:27b"},new{name="clef-flash:latest"}} }));
gateway.MapPost("/api/chat",(JsonObject body)=>{planningRequests.Add(body.DeepClone().AsObject());return Results.Json(new{message=new{content="review công nghệ\nreview mỹ phẩm\nđồ dùng sinh viên"}});});
var systemAnswers=new JsonObject{["fit"]=new JsonObject{["choice"]="recommended"},["topic"]=new JsonObject{["choice"]="topic_0"},["suitability"]=new JsonObject{["score"]=3.6},["reason"]=new JsonObject{["choice"]="on_topic"},["language"]=new JsonObject{["choice"]="vi"},["topic_relation"]=new JsonObject{["choice"]="direct"},["source_risk"]=new JsonObject{["noul"]=0.0},["media_risk"]=new JsonObject{["noul"]=0.0}};
var residentModels=new HashSet<string>();var unloadedModels=new List<string>();var rejectUnload=false;
gateway.MapGet("/api/ps",()=>Results.Json(new{models=residentModels.Select(n=>new{name=n}).ToArray()}));
gateway.MapPost("/api/generate",(JsonObject body)=>{var name=body["model"]!.ToString();unloadedModels.Add(name);if(!rejectUnload)residentModels.Remove(name);return Results.Json(new{});});
gateway.MapPost("/v1/systemone",(JsonObject body)=>{systemRequests.Add(body.DeepClone().AsObject());return Results.Json(new JsonObject{["answers"]=systemAnswers.DeepClone()});});
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
    ["id"] = brandId.ToString(), ["name"] = "Fixture", ["channels"] = new JsonArray(new JsonObject { ["id"] = "fixture-channel", ["name"] = "Fixture channel", ["active"] = true, ["tone"] = "Hóm hỉnh", ["allowedTopics"]="AI thực hành" })
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
    JsonObject Eval(string state,int score,string reason="on_topic",string topic="topic_0",double sourceRisk=0,double mediaRisk=0)=>new(){["status"]=state,["score"]=score,["reason"]="Lý do model",["warnings"]=new JsonArray(),["decision"]=new JsonObject{["language"]=new JsonObject{["choice"]="vi"},["topic_relation"]=new JsonObject{["choice"]="direct"},["fit"]=new JsonObject{["choice"]=state},["reason"]=new JsonObject{["choice"]=reason},["topic"]=new JsonObject{["choice"]=topic},["source_risk"]=new JsonObject{["noul"]=sourceRisk},["media_risk"]=new JsonObject{["noul"]=mediaRisk}}};
    Check(SourceDiscovery.Parse("- review sách\n- review sách\nđồ dùng sinh viên").Length==2,"AI source keywords are deduplicated");
    foreach(var bad in new[]{"https://facebook.com/groups/123/","review sách\n```",""})
    {try{SourceDiscovery.Parse(bad);Check(false,"invalid source plan");}catch(InvalidOperationException){Check(true,"invalid source plan cannot become a URL or empty search");}}
    var articleRecords=new RecordsService(repo,null!,null!);
    var deletedSource=await articleRecords.Delete("articles",articleId);
    Check(deletedSource.Status==200&&JsonNode.Parse(articleDoc.Json)?["deletedAt"] is not null,"delete article moves source to trash without touching Facebook or drafts");
    Check((await articleRecords.Restore("articles",articleId)).Status==200&&JsonNode.Parse(articleDoc.Json)?["deletedAt"] is null,"article can be restored from trash");
    articleDoc.Json=sourceBefore;
    var quotaEvaluation=new CrawlEvaluationState();
    for(var i=0;i<10;i++)quotaEvaluation.Record(ServiceResult.Ok(new JsonObject{["aiStatus"]="rejected",["aiScore"]=25,["aiEvaluatedAt"]=DateTimeOffset.UtcNow.ToString("O")}));
    Check(quotaEvaluation.Retained(10)==0&&!quotaEvaluation.TargetReached(10,10),"ten rejected articles do not meet ten article target");
    for(var i=0;i<10;i++)quotaEvaluation.Record(ServiceResult.Ok(new JsonObject{["aiStatus"]="review",["aiScore"]=60,["aiEvaluatedAt"]=DateTimeOffset.UtcNow.ToString("O")}));
    Check(quotaEvaluation.Retained(20)==10&&quotaEvaluation.TargetReached(20,10),"ten retained replacements meet target after ten rejections");
    Check(!SourceGroupPolicy.MatchesSearch("Công nghệ Sinh học","review công nghệ sinh viên"),"biotechnology group is not a technology review source");
    Check(SourceGroupPolicy.MatchesSearch("Hội Review đồ công nghệ","review công nghệ sinh viên"),"technology review source matches without audience words");
    Check(!SourceGroupPolicy.MatchesSearch("Hội tuyển dụng công nghệ","review công nghệ"),"missing review intent does not match review source");
    Check(SourceGroupPolicy.PoorSource(5,4,0)&&!SourceGroupPolicy.PoorSource(4,4,0)&&!SourceGroupPolicy.PoorSource(5,4,1),"switch poor source only after sufficient consistently rejected sample");
    Check(ArticleDeduplication.NormalizeText("Sách hay… Xem thêm")==ArticleDeduplication.NormalizeText("Sách hay"),"UI see-more suffix does not defeat deduplication");
    Check(ArticleDeduplication.NormalizeText("Sách\u200e hay")==ArticleDeduplication.NormalizeText("Sách hay"),"invisible Facebook formatting does not defeat deduplication");
    Check(ArticleDeduplication.SourceKey("https://www.facebook.com/page/posts/pfbidAbC123/?ref=x")=="post:pfbidAbC123","Facebook opaque post IDs support canonical deduplication");
    var advice=HermesAdvice.Parse(hermesFixture.ToJsonString());
    Check(advice["applied"]!.GetValue<bool>()==false&&advice["keywords"]!.AsArray().Count==2,"Hermes returns bounded advice without applying configuration");
    try{HermesAdvice.Parse("{\"summary\":\"bad\",\"keywords\":[\"https://facebook.com/groups/123/\"]}");Check(false,"Hermes URL rejection");}catch(InvalidOperationException){Check(true,"Hermes cannot fabricate source links in keyword output");}
    Check(HermesAdvice.ParseSelection("{\"summary\":\"Không có bài phù hợp\",\"selectedIds\":[]}")["selectedIds"]!.AsArray().Count==0,"Hermes may select no posts rather than force irrelevant content");
    try{HermesAdvice.ParseSelection("{\"summary\":\"bad\",\"selectedIds\":[\"https://facebook.com/post\"]}");Check(false,"invented post");}catch(InvalidOperationException){Check(true,"Hermes must select feed identifiers rather than invent links");}
    Check(SourceGroupPolicy.MatchesSearch("Hội Review sách hay","nhóm gợi ý sách đọc cho sinh viên"),"book discovery ignores query boilerplate rather than rejecting relevant book groups");
    Check(SourceGroupPolicy.MatchesSearch("Hội Review Công Nghệ","cộng đồng review đồ công nghệ cho sinh viên"),"review technology sources match longer Hermes queries");
    Check(!SourceGroupPolicy.MatchesSearch("Hội Mua Bán Sách Cũ","review sách"),"review discovery does not select book marketplace");
    Check(!SourceGroupPolicy.MatchesSearch("ĐỒ DÙNG HỌC TẬP IN HÌNH SỈ LẺ","đồ dùng học tập"),"review discovery does not select stationery seller group");
    Check(!SourceGroupPolicy.MatchesSearch("Hội mua & bán đồ học tập","kinh nghiệm đồ học tập"),"experience query excludes marketplace with ampersand");
    Check(!SourceGroupPolicy.MatchesSearch("Review Mỹ Phẩm và Skincare","review đồ dùng"),"review intent cannot select cosmetics source");
    Check(SourceGroupPolicy.SearchScore("Hội Review Sách Hay","review sách")>SourceGroupPolicy.SearchScore("CLB Đọc Sách Offline","review sách"),"rank actual review community ahead of offline club");
    Check(SourceGroupPolicy.DiverseKeywords(new[]{"hội đọc sách","review sách","review công nghệ","kinh nghiệm đồ học tập"}).SequenceEqual(new[]{"hội đọc sách","review công nghệ","kinh nghiệm đồ học tập","review sách"}),"interleave distinct topics before repeated book queries");
    Check(SourceGroupPolicy.SourceQuota(10,5)==2&&SourceGroupPolicy.SourceQuota(10,1)==10,"multi-source crawl cannot fill entire target from one community");
    Check(SourceGroupPolicy.Truncated("Review sách\nXem thêm")&&!SourceGroupPolicy.Truncated("Tôi muốn xem thêm sách hay."),"only trailing expansion control marks incomplete caption");
    var crawlToday=new DateOnly(2026,11,3);
    Check(SourceGroupPolicy.ExpiredInvitation("Mời đăng ký offline ngày 17/10/2026",crawlToday),"expired dated invitation cannot enter shortlist");
    Check(!SourceGroupPolicy.ExpiredInvitation("Review cuốn sách tôi đọc ngày 17/10/2026",crawlToday),"past reading date does not exclude evergreen review");
    Check(!SourceGroupPolicy.ExpiredInvitation("Mời đăng ký ngày 10/11/2026",crawlToday),"future event is not considered expired");
    var defaultPolicy=new JsonObject();
    Check(ClefSelectionPolicy.Apply(Eval("review",49,"unverified"),defaultPolicy)["status"]?.ToString()=="rejected","49 is rejected before review exemptions");
    Check(ClefSelectionPolicy.Apply(Eval("review",50),defaultPolicy)["status"]?.ToString()=="review","50 is not automatically rejected");
    Check(ClefSelectionPolicy.Apply(Eval("recommended",43),defaultPolicy)["status"]?.ToString()=="rejected","score below 50 is rejected even when model recommends");
    Check(ClefSelectionPolicy.Apply(Eval("recommended",75),defaultPolicy)["status"]?.ToString()=="recommended","consistent75point article passes default selection threshold");
    Check(ClefSelectionPolicy.Apply(Eval("recommended",81),new JsonObject{["selectionMinScore"]=85})["status"]?.ToString()=="review","brand-specific minimum score is enforced");
    Check(ClefSelectionPolicy.Apply(Eval("recommended",90,topic:"other"),defaultPolicy)["status"]?.ToString()=="review","unknown topic cannot be recommended merely from fit label");
    Check(ClefSelectionPolicy.Apply(Eval("rejected",90),defaultPolicy)["status"]?.ToString()=="review","contradictory rejection and on-topic reason retained for human review");
    Check(ClefSelectionPolicy.Apply(Eval("rejected",90,"off_topic"),defaultPolicy)["status"]?.ToString()=="review","high-score off-topic contradiction does not silently delete article");
    Check(ClefSelectionPolicy.Apply(Eval("rejected",20,"off_topic","other"),defaultPolicy)["status"]?.ToString()=="rejected","consistent irrelevant source remains rejected");
    Check(ClefSelectionPolicy.Apply(Eval("rejected",90,"restricted"),defaultPolicy)["status"]?.ToString()=="rejected","restricted source stays rejected even at high relevance");
    Check(ClefSelectionPolicy.Apply(Eval("rejected",70,"unverified"),defaultPolicy)["status"]?.ToString()=="review","verification need alone does not delete source");
    Check(ClefSelectionPolicy.Apply(Eval("recommended",90,sourceRisk:1),defaultPolicy)["status"]?.ToString()=="review","source risk requires review when brand requires verification");
    Check(ClefSelectionPolicy.Apply(Eval("recommended",90,sourceRisk:1),new JsonObject{["verifyInformation"]=false})["status"]?.ToString()=="recommended","explicit brand verification preference preserved");
    Check(ClefSelectionPolicy.Apply(Eval("recommended",90,mediaRisk:1),defaultPolicy)["status"]?.ToString()=="review","unclear image risk cannot be counted as selected");
    var guarded=ClefSelectionPolicy.Apply(Eval("recommended",43),defaultPolicy);
    Check(ClefSelectionPolicy.Apply(guarded,defaultPolicy).ToJsonString()==guarded.ToJsonString()&&guarded["modelStatus"]?.ToString()=="recommended","gateway/client guard is idempotent and retains raw verdict");
    var english=Eval("recommended",95);english["decision"]!["language"]!["choice"]="en";
    Check(ClefSelectionPolicy.Apply(english,new JsonObject{["language"]="VI"})["status"]?.ToString()=="review","high-score English source never selected for Vietnamese brand");
    Check(ClefSelectionPolicy.Apply(english,new JsonObject{["language"]="EN"})["status"]?.ToString()=="recommended","explicit English brand preference remains supported");
    var indirect=Eval("recommended",95);indirect["decision"]!["topic_relation"]!["choice"]="indirect";
    Check(ClefSelectionPolicy.Apply(indirect,defaultPolicy)["status"]?.ToString()=="review","indirect keyword overlap cannot be selected even with high score");
    var legacy=Eval("recommended",95);legacy["decision"]!.AsObject().Remove("language");
    Check(ClefSelectionPolicy.Apply(legacy,defaultPolicy)["status"]?.ToString()=="review","older gateway without language evidence cannot silently select English content");
    var mixed=Eval("recommended",95);mixed["decision"]!["language"]!["choice"]="mixed";
    Check(ClefSelectionPolicy.Apply(mixed,defaultPolicy)["status"]?.ToString()=="review","substantially bilingual source requires review for Vietnamese channel");
    var localFixtureAi=new AiRunner(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Ollama:BaseUrl",url}}).Build());
    var languageProfile=new JsonObject{["language"]="VI",["allowedTopics"]="Ứng dụng AI và Python"};
    var localVerdict=await localFixtureAi.Evaluate("Mình chia sẻ cách dùng AI và Python để học tập.",languageProfile,[]);
    Check(localVerdict["status"]?.ToString()=="recommended"&&systemRequests[0]["questions"]?["language"]?["type"]?.ToString()=="choice"&&systemRequests[0]["questions"]?["topic_relation"]?["type"]?.ToString()=="choice","production Clef request includes language and direct-relevance questions");
    Check(systemRequests[0]["state"]?["brandPolicy"]?["language"]?.ToString()=="vi","language target normalized before sending to model");
    systemAnswers["language"]!["choice"]="en";
    Check((await localFixtureAi.Evaluate("English content about AI",languageProfile,[]))["status"]?.ToString()=="review","production parser blocks English recommendation despite high model score");
    systemAnswers["language"]!["choice"]="vi";systemAnswers["topic_relation"]!["choice"]="indirect";
    Check((await localFixtureAi.Evaluate("Chỉ nhắc đến một từ AI.",languageProfile,[]))["status"]?.ToString()=="review","production parser blocks indirect recommendation");
    var planned=await localFixtureAi.Rewrite(new RewriteRequest("{\"allowedTopics\":\"review đồ dùng sinh viên\"}",Mode:"source-keywords",Temperature:0.2));
    Check(SourceDiscovery.Parse(planned).Length==3&&planningRequests.Single()["messages"]![0]!["content"]!.ToString().Contains("tìm nhóm Facebook nguồn")&&!planningRequests.Single()["messages"]![1]!["content"]!.ToString().Contains("Bài viết mới"),"production source planning uses dedicated prompt, not Facebook rewrite prompt");
    residentModels.Clear();residentModels.Add("clef-flash:latest");unloadedModels.Clear();
    await localFixtureAi.Evaluate("same model",languageProfile,[]);
    Check(unloadedModels.Count==0&&systemRequests.Last()["keep_alive"]!.GetValue<int>()==600,"Clef stays resident across evaluations with ten minute idle keepalive");
    await localFixtureAi.Rewrite(new RewriteRequest("review sách",Mode:"source-keywords"));
    Check(unloadedModels.SequenceEqual(new[]{"clef-flash:latest"})&&planningRequests.Last()["keep_alive"]!.GetValue<int>()==600,"switch to Bonsai unloads only Clef before inference and keeps Bonsai warm");
    residentModels.Clear();residentModels.Add("bonsai-2:27b");unloadedModels.Clear();
    await localFixtureAi.Rewrite(new RewriteRequest("review sách",Mode:"source-keywords"));
    Check(unloadedModels.Count==0,"consecutive Bonsai requests do not unload resident Bonsai");
    rejectUnload=true;var priorCalls=systemRequests.Count;
    try{await localFixtureAi.Evaluate("blocked switch",languageProfile,[]);Check(false,"switch must stop");}catch(InvalidOperationException){Check(systemRequests.Count==priorCalls,"failed model unload blocks inference of another model");}
    rejectUnload=false;residentModels.Clear();
    var hermesConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Ollama:BaseUrl"]=url,["Hermes:Enabled"]="true",["Hermes:AdapterUrl"]=url,["Hermes:AdapterKey"]=new string('x',40),["Hermes:Model"]="bonsai-2:27b"}).Build();
    var hermesAi=new AiRunner(hermesConfig);
    var hermesOutput=HermesAdvice.Parse(await hermesAi.Rewrite(new RewriteRequest("review sách",Mode:"hermes-config")));
    Check(hermesOutput["applied"]!.GetValue<bool>()==false&&hermesRequests.Single()["prompt"]!.ToString().Contains("Không dùng công cụ"),"Hermes request reaches isolated agent adapter with advisory-only contract");
    systemAnswers["language"]!["choice"]="invalid";
    try{await localFixtureAi.Evaluate("test",languageProfile,[]);Check(false,"invalid language");}catch(AiGatewayException ex){Check(ex.Code=="CLEF_INVALID_OUTPUT","malformed language answer is rejected rather than treated as valid selection");}
    Check(BrandPolicy.Validate(new JsonObject{["selectionMinScore"]=101}) is not null,"invalid selection threshold rejected");
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
    var autoConfig=JsonNode.Parse(CrawlConfig().ToJsonString())!.AsObject();autoConfig["sourceMode"]="ai-auto";autoConfig["groupKeywords"]="";
    var autoResult=await workflows.StartCrawl(autoConfig);
    Check(autoResult.Status==200&&autoConfig["sourceUrls"]!.AsArray().Count==2,"AI auto uses configured brand sources without manual keywords");
    var savedBrand=brandRow.Json;brandData.Remove("crawlSourceUrls");brandRow.Json=brandData.ToJsonString();
    var discoverConfig=JsonNode.Parse(CrawlConfig().ToJsonString())!.AsObject();discoverConfig["sourceMode"]="ai-auto";discoverConfig["groupKeywords"]="ignored-client-keyword";
    var discoverResult=await workflows.StartCrawl(discoverConfig);
    Check(discoverResult.Status==200&&discoverConfig["autoDiscover"]!.GetValue<bool>()&&discoverConfig["groupKeywords"]!.ToString()==""&&discoverConfig["discoveryProfile"] is JsonObject,"AI mode without links queues discovery from server profile instead of requiring links or trusting client keywords");
    var linksOnly=JsonNode.Parse(CrawlConfig().ToJsonString())!.AsObject();
    Check((await workflows.StartCrawl(linksOnly)).Status==400,"explicit brand-links mode still requires saved links");
    brandRow.Json=savedBrand;
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
    var assessment=JsonNode.Parse(articleDoc.Json)!.AsObject();assessment["mediaWarnings"]=new JsonArray("Ảnh là bản Facebook cung cấp trên feed; chưa xác minh độ phân giải gốc.");articleDoc.Json=assessment.ToJsonString();
    var evaluatedSource=await service.EvaluateArticle(articleId);
    Check(evaluatedSource.Status==200&&((JsonObject)evaluatedSource.Value!)["aiStatus"]?.ToString()=="recommended","feed resolution warning alone does not demote a readable image article");
    Check(evaluations.Count==2&&evaluations[1]["profile"]?["name"]?.ToString()=="Fixture"&&evaluations[1]["profile"]?["channelName"]?.ToString()=="Fixture channel"&&evaluations[1]["profile"]?["allowedTopics"]?.ToString()=="AI thực hành","URL source without legacy flag still assessed against channel-specific topics");
    assessment=JsonNode.Parse(articleDoc.Json)!.AsObject();assessment["media"]=new JsonArray(new JsonObject{["type"]="image",["url"]="/media/missing.png"});articleDoc.Json=assessment.ToJsonString();
    var missingMediaEvaluation=await service.EvaluateArticle(articleId);
    Check(missingMediaEvaluation.Status==200&&((JsonObject)missingMediaEvaluation.Value!)["aiStatus"]?.ToString()=="review","unreadable source media still requires human review");
    var manualDraft=(JsonObject)(await service.CreateDraft(articleId,Request("repost"))).Value!;
    var manualId=Guid.Parse(manualDraft["id"]!.ToString());var beforeMark=repo.Rows.Single(d=>d.Id==manualId).Json;
    var postedAt=DateTimeOffset.UtcNow.AddMinutes(-5);
    JsonObject PublishedRequest(string link="https://www.facebook.com/groups/123/posts/999/",string? at=null,bool confirmed=true)=>new(){["publishedUrl"]=link,["publishedAt"]=at??postedAt.ToString("O"),["confirmed"]=confirmed,["expectedVersion"]=manualDraft["createdAt"]!.ToString()};
    Check((await service.MarkPublished(manualId,PublishedRequest(at:DateTimeOffset.UtcNow.AddDays(1).ToString("O")),"fixture-user")).Status==400,"future publication timestamp rejected");
    Check((await service.MarkPublished(manualId,PublishedRequest(at:"2026-11-01T12:00:00"),"fixture-user")).Status==400,"manual timestamp requires explicit timezone");
    Check((await service.MarkPublished(manualId,PublishedRequest(confirmed:false),"fixture-user")).Status==400&&repo.Rows.Single(d=>d.Id==manualId).Json==beforeMark,"unconfirmed request has no side effects");
    var staleMark=PublishedRequest();staleMark["expectedVersion"]="stale";Check((await service.MarkPublished(manualId,staleMark,"fixture-user")).Status==409,"stale client cannot mark different saved content");
    var marked=await service.MarkPublished(manualId,PublishedRequest(),"fixture-user");
    Check(marked.Status==200&&((JsonObject)marked.Value!)["status"]?.ToString()=="published"&&((JsonObject)marked.Value!)["publicationEvidence"]?.ToString()=="user_reported","mark publication saves manual evidence without Facebook or publisher account");
    var firstPublishedJson=repo.Rows.Single(d=>d.Id==manualId).Json;
    Check((await service.MarkPublished(manualId,PublishedRequest(),"fixture-user")).Status==200&&repo.Rows.Single(d=>d.Id==manualId).Json==firstPublishedJson,"same manual record is idempotent and cannot double-count");
    Check((await service.MarkPublished(manualId,PublishedRequest("https://www.facebook.com/groups/123/posts/1000/"),"fixture-user")).Status==409,"published evidence cannot be silently overwritten");
    Check((await service.SaveDraft(manualId,new JsonObject{["content"]="overwrite"})).Status==409,"published draft cannot revert to editable through save endpoint");
    var report=System.Text.Json.JsonSerializer.SerializeToNode((await new ContentReportService(repo).Overview(postedAt.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd"))).Value)!;
    Check(report["published"]?.GetValue<int>()==1,"manual publication increments report on actual publication date");
    var directRequest=PublishedRequest("https://www.facebook.com/groups/123/posts/1001/");directRequest["channelId"]="fixture-channel";
    var countBeforeDirect=repo.Rows.Count(r=>r.Kind=="drafts");var aiCallsBeforeDirect=calls.Count;
    var direct=await service.MarkArticlePublished(articleId,directRequest,"fixture-user");
    Check(direct.Status==200&&repo.Rows.Count(r=>r.Kind=="drafts")==countBeforeDirect+1&&((JsonObject)direct.Value!)["status"]?.ToString()=="published"&&calls.Count==aiCallsBeforeDirect,"source article can be marked directly without prior draft or AI call");
    Check((await service.MarkArticlePublished(articleId,directRequest,"fixture-user")).Status==200&&repo.Rows.Count(r=>r.Kind=="drafts")==countBeforeDirect+1,"repeated source mark cannot create duplicate publication records");
    var invalidDirect=PublishedRequest("https://www.facebook.com/groups/123/posts/1002/");invalidDirect["channelId"]="missing";
    Check((await service.MarkArticlePublished(articleId,invalidDirect,"fixture-user")).Status==400&&repo.Rows.Count(r=>r.Kind=="drafts")==countBeforeDirect+1,"invalid source target leaves no intermediate draft");
    var noLink=PublishedRequest("");noLink["channelId"]="fixture-channel";
    var noLinkResult=await service.MarkArticlePublished(articleId,noLink,"fixture-user");
    Check(noLinkResult.Status==200&&((JsonObject)noLinkResult.Value!)["publishedUrl"] is null,"manual mark accepts no link");
    var afterNoLink=repo.Rows.Count;
    Check((await service.MarkArticlePublished(articleId,noLink,"fixture-user")).Status==200&&repo.Rows.Count==afterNoLink,"no-link mark remains idempotent");
    var noLinkReport=System.Text.Json.JsonSerializer.SerializeToNode((await new ContentReportService(repo).Overview(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd"))).Value)!;
    Check(noLinkReport["published"]?.GetValue<int>()==3,"report counts explicit manual evidence without link");
    var tasksBeforeEmptySearch=repo.Rows.Count;
    var emptySearch=await new WorkflowsService(repo,null!,null!).StartCrawl(new JsonObject{["brandId"]=brandId.ToString(),["sourceMode"]="group-search",["groupKeywords"]=""});
    Check(emptySearch.Status==400&&repo.Rows.Count==tasksBeforeEmptySearch,"empty group search never falls back to editorial topics or queues a task");
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
    public Task<Document> SaveManualPublicationAsync(Document record,string auditMessage){var existing=Rows.FirstOrDefault(d=>d.Id==record.Id&&d.Kind=="drafts");if(existing is not null)return Task.FromResult(existing);Rows.Add(record);return Task.FromResult(record);}
    public Task<bool> TryUpdateDraftAsync(Guid id,string expectedJson,string updatedJson,string auditMessage){var row=Rows.FirstOrDefault(d=>d.Kind=="drafts"&&d.Id==id&&d.Json==expectedJson);if(row is null)return Task.FromResult(false);row.Json=updatedJson;return Task.FromResult(true);}
    public Task<bool> DeleteTaskAsync(Guid id)=>Task.FromResult(Rows.RemoveAll(d=>d.Kind=="tasks"&&d.Id==id)>0);
    public Task<bool> TryUpdateTaskAsync(Guid id,string expectedJson,string updatedJson,string auditMessage){var row=Rows.FirstOrDefault(d=>d.Kind=="tasks"&&d.Id==id&&d.Json==expectedJson);if(row is null)return Task.FromResult(false);row.Json=updatedJson;return Task.FromResult(true);}
    public Task<bool> TryUpdateBrandAsync(Guid id, string expectedJson, string updatedJson, string auditMessage) => throw new NotSupportedException();
}
