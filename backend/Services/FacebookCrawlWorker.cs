using Microsoft.Playwright;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace SammiCrawl.Backend.Services;

public sealed class FacebookCrawlWorker(IServiceScopeFactory scopes, IDataProtectionProvider protection, IWebHostEnvironment env, ILogger<FacebookCrawlWorker> log) : BackgroundService
{
    readonly Channel<Guid> queue = Channel.CreateUnbounded<Guid>();
    readonly HttpClient mediaHttp = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan };
    public override void Dispose() { mediaHttp.Dispose(); base.Dispose(); }
    int busy;
    public bool IsBusy => Volatile.Read(ref busy) != 0;
    public bool Enqueue(Guid id) { if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) return false; queue.Writer.TryWrite(id); return true; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A restart cannot silently replay a partly completed Facebook request.
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var articles = (await db.ListDocumentsAsync("articles")).Select(d => JsonNode.Parse(d.Json)!.AsObject())
                .Where(a => a["crawlTaskId"] is not null).GroupBy(a => a["crawlTaskId"]!.ToString())
                .ToDictionary(g => g.Key, g => g.ToList());
            foreach (var d in await db.ListDocumentsAsync("tasks"))
            {
                var j = JsonNode.Parse(d.Json)!.AsObject();
                if (j["workerNode"]?.ToString() == "Facebook local" && articles.TryGetValue(d.Id.ToString(), out var ownArticles)
                    && CrawlEvaluationState.RepairIncompleteCompletion(j, ownArticles))
                {
                    d.Json = j.ToJsonString();
                    db.AddLog(new Audit { Message = $"Đã sửa trạng thái tác vụ {d.Id}: Clef chưa đánh giá xong; không tự cào lại." });
                }
                if (j["workerNode"]?.ToString() == "Facebook local" && j["status"]?.ToString() is "pending" or "running") { j["status"]="paused";j["errorReason"]="Ứng dụng khởi động lại; không tự chạy lại tác vụ.";d.Json=j.ToJsonString(); }
            }
            await db.SaveChangesAsync();
        }
        await foreach (var id in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try { await Run(id, stoppingToken); }
            catch (OperationCanceledException) { await State(id, "paused", "Tác vụ đã dừng."); }
            catch (Exception ex) { log.LogWarning("Crawl {JobId} failed: {Type}", id, ex.GetType().Name); await State(id,"failed","Không hoàn tất thu thập; xem mã lỗi và số bài đã lưu.","CRAWL_FAILED"); }
            finally { Interlocked.Exchange(ref busy, 0); }
        }
    }
    async Task State(Guid id,string status,string message,string? code=null,int? count=null,int? duplicates=null,CrawlEvaluationState? evaluation=null)
    {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IProjectRepository>();var d=await db.FindDocumentAsync(id,"tasks");if(d is null)return;
        var j=JsonNode.Parse(d.Json)!.AsObject();if(j["status"]?.ToString()=="paused"&&status=="running")throw new OperationCanceledException();
        j["status"]=status;j["errorReason"]=message;j["errorCode"]=code;
        evaluation?.WriteTo(j);
        if(count.HasValue)j["crawledPosts"]=count.Value;
        j["progress"]=CrawlEvaluationState.Progress(status,j["crawledPosts"]?.GetValue<int>()??0,j["maxPosts"]?.GetValue<int>()??1,j["clefEvaluatedPosts"]?.GetValue<int>()??0);
        if(duplicates.HasValue)j["duplicatesSkipped"]=duplicates.Value;
        d.Json=j.ToJsonString();db.AddLog(new Audit{Message=$"Cào {id}: {message}"});await db.SaveChangesAsync();
    }
    async Task<bool> Cancelled(Guid id)
    {using var s=scopes.CreateScope();var d=await s.ServiceProvider.GetRequiredService<IProjectRepository>().FindDocumentAsync(id,"tasks");return d is null||JsonNode.Parse(d.Json)?["status"]?.ToString()=="paused";}
    async Task Delay(Guid id,int ms,CancellationToken ct){var watch=System.Diagnostics.Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){if(await Cancelled(id))throw new OperationCanceledException();var remaining=ms-watch.ElapsedMilliseconds;if(remaining>0)await Task.Delay((int)Math.Min(remaining,1000),ct);}}
    async Task<FacebookCheck?> Blocked(IPage page)
    {
        var text=(await page.Locator("body").InnerTextAsync()).ToLowerInvariant();
        if(page.Url.Contains("checkpoint")||page.Url.Contains("two_step_verification"))return FacebookAccountHealth.Runtime("CHECKPOINT");
        if(page.Url.Contains("/login")||await page.Locator("input[name=email]").CountAsync()>0)return FacebookAccountHealth.Runtime("LOGIN_REQUIRED");
        if(text.Contains("temporarily blocked")||text.Contains("you're going too fast"))return FacebookAccountHealth.Runtime("RATE_LIMITED");
        return null;
    }
    async Task<bool> CheckSession(IPage page, Guid taskId, Guid accountId, int count, int duplicates)
    {
        var blocked = await Blocked(page); if (blocked is null) return true;
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var account = await db.FindDocumentAsync(accountId, "accounts");
        if (account is not null) { var data = JsonNode.Parse(account.Json)!.AsObject(); FacebookAccountHealth.Apply(data, blocked, "runtime_error"); account.Json = data.ToJsonString(); await db.SaveChangesAsync(); }
        await State(taskId, "paused", blocked.Reason, blocked.Code, count, duplicates); return false;
    }
    async Task<List<JsonObject>?> DiscoverGroups(IPage page, JsonObject job, Guid taskId, Guid accountId, int count, int duplicates, CancellationToken ct)
    {
        var found = new Dictionary<string, JsonObject>(); var limit = job["maxGroups"]!.GetValue<int>();
        foreach (var keyword in SourceGroupPolicy.Keywords(job["groupKeywords"]?.ToString()))
        {
            if (await Cancelled(taskId)) throw new OperationCanceledException();
            await State(taskId, "running", $"Đang tìm nhóm theo từ khóa: {keyword}", count:count, duplicates:duplicates);
            await page.GotoAsync("https://www.facebook.com/search/groups/?q=" + Uri.EscapeDataString(keyword), new() { WaitUntil=WaitUntilState.DOMContentLoaded, Timeout=45000 });
            await Delay(taskId, 2500, ct);
            for (var scroll = 0; scroll < 3 && found.Count < limit; scroll++)
            {
                if (!await CheckSession(page, taskId, accountId, count, duplicates)) return null;
                var links = await page.EvaluateAsync<string>("""
                    () => JSON.stringify(Array.from(document.querySelectorAll('a[href*="/groups/"]')).map(a=>({name:(a.innerText||a.getAttribute('aria-label')||'').trim(),url:a.href})).filter(a=>a.name.length>1))
                    """);
                foreach (var item in JsonNode.Parse(links)!.AsArray())
                {
                    var url = SourceGroupPolicy.CanonicalUrl(item?["url"]?.ToString());
                    if (url is null || found.ContainsKey(url)) continue;
                    found[url] = new JsonObject { ["url"]=url, ["name"]=item!["name"]!.ToString()[..Math.Min(300,item["name"]!.ToString().Length)], ["keyword"]=keyword };
                    if (found.Count >= limit) break;
                }
                if (found.Count < limit) { await page.Mouse.WheelAsync(0, 800); await Delay(taskId, 1800, ct); }
            }
            if (found.Count >= limit) break;
        }
        return found.Values.ToList();
    }
    async Task Run(Guid id,CancellationToken ct)
    {
        JsonObject job;List<Guid> accountIds;
        using(var s=scopes.CreateScope()){var d=await s.ServiceProvider.GetRequiredService<IProjectRepository>().FindDocumentAsync(id,"tasks");job=JsonNode.Parse(d!.Json)!.AsObject();accountIds=job["crawlAccountIds"]!.AsArray().Select(n=>Guid.Parse(n!.ToString())).ToList();}
        var target=job["url"]!.ToString();var searchGroups=job["sourceMode"]?.ToString()=="group-search";var groupCounts=new Dictionary<string,int>();var visitedGroups=new HashSet<string>();var brandId=Guid.Parse(job["brandId"]!.ToString());var max=job["maxPosts"]!.GetValue<int>();var count=0;var dup=0;var evaluation=new CrawlEvaluationState();var seen=new HashSet<string>();
        await State(id,"running","Bắt đầu thu thập bằng phiên Facebook đã lưu.",count:0);
        using var pw=await Playwright.CreateAsync();await using var browser=await pw.Chromium.LaunchAsync(new(){Headless=true,Timeout=30000});
        foreach(var accountId in accountIds)
        {
            if(count>=max)break;
            string raw;
            using(var s=scopes.CreateScope())
            {
                var db=s.ServiceProvider.GetRequiredService<IProjectRepository>();var a=await db.FindDocumentAsync(accountId,"accounts");var aj=a is null?null:JsonNode.Parse(a.Json);
                if(a?.ProtectedSecret is null||aj?["deletedAt"] is not null||aj?["role"]?.ToString()!="crawl"||aj?["enabled"]?.GetValue<bool>()==false)continue;
                raw=protection.CreateProtector("FacebookSession.v1").Unprotect(a.ProtectedSecret);
            }
            var cookies=new List<Cookie>();
            if(raw.TrimStart().StartsWith('[')||raw.TrimStart().StartsWith('{')){var node=JsonNode.Parse(raw);foreach(var c in node as JsonArray??node?["cookies"]!.AsArray()??new JsonArray())if(c?["name"] is not null)cookies.Add(new(){Name=c["name"]!.ToString(),Value=c["value"]!.ToString(),Domain=".facebook.com",Path="/",Secure=true});}
            else foreach(var item in raw.Split(';')){var pair=item.Trim().Split('=',2);if(pair.Length==2)cookies.Add(new(){Name=pair[0],Value=pair[1],Domain=".facebook.com",Path="/",Secure=true});}
            await using var context=await browser.NewContextAsync(new(){Locale="vi-VN",ViewportSize=new(){Width=1365,Height=900}});await context.AddCookiesAsync(cookies);
            var page=await context.NewPageAsync();
            var targets=searchGroups ? await DiscoverGroups(page,job,id,accountId,count,dup,ct) : new List<JsonObject> { new() { ["url"]=target } };
            if(targets is null)return;
            var perAccount=0;
            foreach(var group in targets)
            {
            if(count>=max||perAccount>=job["accountPostLimit"]!.GetValue<int>())break;
            target=group["url"]!.ToString();
            if(searchGroups && !visitedGroups.Contains(target) && visitedGroups.Count>=job["maxGroups"]!.GetValue<int>())continue;
            visitedGroups.Add(target);
            var groupLimit=searchGroups?job["groupPostLimit"]!.GetValue<int>():max;
            if(groupCounts.GetValueOrDefault(target)>=groupLimit)continue;
            Guid? sourceGroupId=null;
            if(searchGroups)
            {
                using var scope=scopes.CreateScope();
                sourceGroupId=await scope.ServiceProvider.GetRequiredService<IProjectRepository>().TrackSourceGroupAsync(brandId,group["name"]!.ToString(),target,group["keyword"]!.ToString(),accountId,id);
                await State(id,"running",$"Đang thu thập nhóm {group["name"]}; sẽ đánh giá Clef và đối chiếu các kênh.",count:count,duplicates:dup,evaluation:evaluation);
            }
            await page.GotoAsync(target,new(){WaitUntil=WaitUntilState.DOMContentLoaded,Timeout=45000});await Delay(id,2500,ct);
            if(!await CheckSession(page,id,accountId,count,dup))return;
            if(searchGroups)
            {
                var body=(await page.Locator("body").InnerTextAsync()).ToLowerInvariant();
                if(body.Contains("this content isn't available")||body.Contains("nội dung này hiện không hiển thị")||body.Contains("join this group to see"))
                { await State(id,"running",$"Không đọc được nội dung nhóm {group["name"]}; bỏ qua, không tự tham gia nhóm.",count:count,duplicates:dup,evaluation:evaluation);continue; }
            }
            for(var scroll=0;scroll<job["maxScrolls"]!.GetValue<int>()&&count<max&&perAccount<job["accountPostLimit"]!.GetValue<int>()&&groupCounts.GetValueOrDefault(target)<groupLimit;scroll++)
            {
                if(await Cancelled(id))throw new OperationCanceledException();
                if(!await CheckSession(page,id,accountId,count,dup))return;
                // Expand only visible post text; never click reactions, shares or join controls.
                var more=page.GetByRole(AriaRole.Button,new(){Name="Xem thêm",Exact=true});
                for(var k=0;k<Math.Min(await more.CountAsync(),12);k++){try{if(await more.Nth(k).IsVisibleAsync())await more.Nth(k).ClickAsync(new(){Timeout=1500});}catch{/* text may have rerendered */}}
                var metadataCapturedAt = DateTimeOffset.UtcNow;
                var json=await page.EvaluateAsync<string>("(() => { const metadata = " + FacebookPostMetadata.Script + "; return (" + """
                () => JSON.stringify(Array.from(document.querySelectorAll('h2')).map(h=>{
                  let n=h;
                  for(let i=0;n&&i<14;i++,n=n.parentElement){
                    if(!n.querySelector('[aria-label="Bày tỏ cảm xúc"],[aria-label="Like"],[aria-label="Thích"]'))continue;
                    const links=Array.from(n.querySelectorAll('a')).map(a=>a.href);
                    let source=links.find(u=>/\/(posts|permalink)\//.test(u)||u.includes('story_fbid='));
                    if(!source){const photo=links.find(u=>u.includes('set=gm.'));if(photo){const m=photo.match(/set=gm\.(\d+)/);const g=location.pathname.match(/groups\/([^/]+)/);if(m&&g)source='https://www.facebook.com/groups/'+g[1]+'/posts/'+m[1]+'/';}}
                    if(!source)source=links.find(u=>/\/(reel|videos)\/\d+/.test(u));
                    if(!source)return null;
                    let message=n.querySelector('[data-ad-preview="message"],[data-ad-comet-preview="message"],[data-ad-rendering-role="story_message"]');
                    let text=message?.innerText;
                    if(!text){text=n.innerText;let split=text.indexOf('·\n');if(split>=0)text=text.slice(split+2);else{text=text.replace(/^(Facebook\n)+/,'');const lines=text.split('\n');const author=lines.indexOf(h.innerText);if(author>=0)text=lines.slice(author+2).join('\n');}text=text.split('Đã chia sẻ bài viết')[0].split('Shared post')[0];}
                    text=text.replace(/Ẩn bớt|See less/g,'').trim();
                    return {sourceUrl:source,content:text,author:h.innerText,metadata:metadata(n,source),images:Array.from(n.querySelectorAll('img')).filter(i=>i.naturalWidth>200).map(i=>i.currentSrc),videos:Array.from(n.querySelectorAll('video')).map(v=>v.currentSrc)};
                  }return null;
                }).filter(Boolean))
                """ + ")(); })()");
                var candidates=JsonNode.Parse(json)!.AsArray();if(candidates.Count==0&&scroll%5==0)await State(id,"running",$"Chưa nhận diện bài mới ở lần cuộn {scroll+1}; tiếp tục trong giới hạn đã đặt.",count:count,duplicates:dup);
                foreach(var candidate in candidates)
                {
                    if(count>=max||perAccount>=job["accountPostLimit"]!.GetValue<int>()||groupCounts.GetValueOrDefault(target)>=groupLimit)break;
                    var source=candidate!["sourceUrl"]!.ToString();var sourceKey=ArticleDeduplication.SourceKey(source);if(sourceKey is null||!seen.Add(sourceKey))continue;
                    // Delay between processed posts, including duplicates; this is not anti-detection evasion.
                    if(seen.Count>1)await Delay(id,Random.Shared.Next(job["delayMinMs"]!.GetValue<int>(),job["delayMaxMs"]!.GetValue<int>()+1),ct);
                    var keywords=(job["keywords"]?.ToString()??"").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                    if(keywords.Length>0&&!keywords.Any(k=>candidate["content"]!.ToString().Contains(k,StringComparison.OrdinalIgnoreCase)))continue;
                    var media=new JsonArray();var warnings=new JsonArray();if(candidate["images"]!.AsArray().Count>0)warnings.Add("Ảnh là bản Facebook cung cấp trên feed; chưa xác minh độ phân giải gốc.");
                    foreach(var src in candidate["images"]!.AsArray().Select(n=>n!.ToString()).Distinct().Take(12))
                    {var m=await Download(context,src,"image",id,ct);if(m is not null)media.Add(m);else warnings.Add("Không tải được một ảnh của bài nguồn.");}
                    foreach(var src in candidate["videos"]!.AsArray().Select(n=>n!.ToString()).Distinct().Take(4))
                    {var m=await Download(context,src,"video",id,ct);if(m is not null)media.Add(m);else warnings.Add("Video chưa tải được file gốc; không coi media là đầy đủ.");}
                    var article=new JsonObject{["content"]=candidate["content"]!.ToString(),["title"]=candidate["content"]!.ToString()[..Math.Min(100,candidate["content"]!.ToString().Length)],["sourceUrl"]=source,["sourceAccountId"]=accountId.ToString(),["crawlTaskId"]=id.ToString(),["media"]=media,["mediaWarnings"]=warnings,["aiStatus"]="pending",["sourceAuthor"]=candidate["author"]!.ToString(),["crawlTopicDescription"]=job["topicDescription"]?.ToString()};
                    FacebookPostMetadata.Apply(article, candidate["metadata"], metadataCapturedAt);
                    if(sourceGroupId.HasValue) { article["sourceGroupId"]=sourceGroupId.Value.ToString(); article["sourceGroupName"]=group["name"]!.ToString(); article["sourceGroupUrl"]=target; article["channelEvaluationRequired"]=true; }
                    using(var s=scopes.CreateScope())
                    {
                        var result=await s.ServiceProvider.GetRequiredService<IProjectRepository>().IngestArticleAsync(brandId,article);
                        if(result.Duplicate){dup++;foreach(var m in media){var path=BrandPolicy.LocalMedia(env.ContentRootPath,m?["url"]?.ToString());if(path is not null)File.Delete(path);}}
                        else
                        {
                            count++;perAccount++;groupCounts[target]=groupCounts.GetValueOrDefault(target)+1;
                            await State(id,"running",$"Đã thu thập {count}/{max} bài; đang đánh giá Clef bài vừa thu thập.",count:count,duplicates:dup,evaluation:evaluation);
                            if(await Cancelled(id))throw new OperationCanceledException();
                            var assessed=await s.ServiceProvider.GetRequiredService<IContentService>().EvaluateArticle(result.Record.Id);
                            if(await Cancelled(id))throw new OperationCanceledException();
                            if(!evaluation.Record(assessed))
                            {
                                var reason=(assessed.Value as JsonObject)?["error"]?.ToString()
                                    ?? (assessed.Value is null ? null : System.Text.Json.JsonSerializer.SerializeToNode(assessed.Value)?["error"]?.ToString())
                                    ?? "Clef chưa trả kết quả hợp lệ.";
                                await State(id,"paused",$"Đã thu thập {count}/{max} bài; Clef đánh giá {evaluation.Evaluated}/{count}. {reason} Bài chưa đánh giá được giữ chờ xử lý; tác vụ chưa hoàn thành.","CLEF_EVALUATION_INCOMPLETE",count,dup,evaluation);
                                return;
                            }
                        }
                    }
                    await State(id,"running",$"Đã thu thập {count}/{max} bài; Clef {evaluation.Evaluated}/{count}: phù hợp {evaluation.Recommended}, cần xem lại {evaluation.Review}, đã loại {evaluation.Rejected}; bỏ qua {dup} bài trùng.",count:count,duplicates:dup,evaluation:evaluation);
                }
                if(count<max){await page.Mouse.WheelAsync(0,850);await Delay(id,Random.Shared.Next(1500,3001),ct);}
            }
            }
            if(count<max)await Delay(id,Random.Shared.Next(job["accountRestMinMs"]!.GetValue<int>(),job["accountRestMaxMs"]!.GetValue<int>()+1),ct);
        }
        var complete=count==max&&evaluation.Evaluated==count;
        await State(id,complete?"completed":"paused",$"Đã thu thập {count}/{max} bài; Clef đánh giá {evaluation.Evaluated}/{count}: phù hợp {evaluation.Recommended}, cần xem lại {evaluation.Review}, đã loại {evaluation.Rejected}; bỏ qua {dup} bài trùng. Không đăng Facebook.",complete?null:"SOURCE_EXHAUSTED",count,dup,evaluation);
    }
    async Task<JsonObject?> Download(IBrowserContext context,string src,string kind,Guid job,CancellationToken ct)
    {
        if (!Uri.TryCreate(src, UriKind.Absolute, out var url) || !FacebookMediaDownload.Allowed(url)) return null;
        try
        {
            var scopedCookies = await context.CookiesAsync(new[] { src });
            var cookie = string.Join("; ", scopedCookies.Select(c => c.Name + "=" + c.Value));
            var agent = context.Pages.Count > 0 ? await context.Pages[0].EvaluateAsync<string>("navigator.userAgent") : "";
            var folder = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "media", "crawl", job.ToString());
            var result = await FacebookMediaDownload.Download(mediaHttp, url, folder, kind, cookie, agent, ct);
            if (result is null) return null;
            return new() { ["type"] = kind, ["url"] = $"/media/crawl/{job}/{result.Value.FileName}", ["bytes"] = result.Value.Bytes, ["sourceUrl"] = src, ["quality"] = "source_served" };
        }
        catch { return null; }
    }
}
