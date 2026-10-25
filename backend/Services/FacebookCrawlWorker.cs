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
            foreach (var d in await db.ListDocumentsAsync("tasks"))
            {
                var j = JsonNode.Parse(d.Json)!.AsObject();
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
    async Task State(Guid id,string status,string message,string? code=null,int? count=null,int? duplicates=null)
    {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IProjectRepository>();var d=await db.FindDocumentAsync(id,"tasks");if(d is null)return;
        var j=JsonNode.Parse(d.Json)!.AsObject();if(j["status"]?.ToString()=="paused"&&status=="running")throw new OperationCanceledException();
        j["status"]=status;j["errorReason"]=message;j["errorCode"]=code;
        if(count.HasValue){j["crawledPosts"]=count.Value;j["progress"]=(int)(100.0*count.Value/(j["maxPosts"]?.GetValue<int>()??1));}
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
    async Task Run(Guid id,CancellationToken ct)
    {
        JsonObject job;List<Guid> accountIds;
        using(var s=scopes.CreateScope()){var d=await s.ServiceProvider.GetRequiredService<IProjectRepository>().FindDocumentAsync(id,"tasks");job=JsonNode.Parse(d!.Json)!.AsObject();accountIds=job["crawlAccountIds"]!.AsArray().Select(n=>Guid.Parse(n!.ToString())).ToList();}
        var target=job["url"]!.ToString();var brandId=Guid.Parse(job["brandId"]!.ToString());var max=job["maxPosts"]!.GetValue<int>();var count=0;var dup=0;var savedIds=new List<Guid>();var seen=new HashSet<string>();
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
            var page=await context.NewPageAsync();await page.GotoAsync(target,new(){WaitUntil=WaitUntilState.DOMContentLoaded,Timeout=45000});await page.WaitForTimeoutAsync(2500);
            var perAccount=0;
            for(var scroll=0;scroll<job["maxScrolls"]!.GetValue<int>()&&count<max&&perAccount<job["accountPostLimit"]!.GetValue<int>();scroll++)
            {
                if(await Cancelled(id))throw new OperationCanceledException();
                var blocked=await Blocked(page);if(blocked is not null)
                {
                    using var s=scopes.CreateScope();var db=s.ServiceProvider.GetRequiredService<IProjectRepository>();var a=await db.FindDocumentAsync(accountId,"accounts");var aj=JsonNode.Parse(a!.Json)!.AsObject();FacebookAccountHealth.Apply(aj,blocked,"runtime_error");a.Json=aj.ToJsonString();await db.SaveChangesAsync();await State(id,"paused",blocked.Reason,blocked.Code,count,dup);return;
                }
                // Expand only visible post text; never click reactions, shares or join controls.
                var more=page.GetByRole(AriaRole.Button,new(){Name="Xem thêm",Exact=true});
                for(var k=0;k<Math.Min(await more.CountAsync(),12);k++){try{if(await more.Nth(k).IsVisibleAsync())await more.Nth(k).ClickAsync(new(){Timeout=1500});}catch{/* text may have rerendered */}}
                var json=await page.EvaluateAsync<string>("""
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
                    return {sourceUrl:source,content:text,author:h.innerText,images:Array.from(n.querySelectorAll('img')).filter(i=>i.naturalWidth>200).map(i=>i.currentSrc),videos:Array.from(n.querySelectorAll('video')).map(v=>v.currentSrc)};
                  }return null;
                }).filter(Boolean))
                """);
                var candidates=JsonNode.Parse(json)!.AsArray();if(candidates.Count==0&&scroll%5==0)await State(id,"running",$"Chưa nhận diện bài mới ở lần cuộn {scroll+1}; tiếp tục trong giới hạn đã đặt.",count:count,duplicates:dup);
                foreach(var candidate in candidates)
                {
                    if(count>=max||perAccount>=job["accountPostLimit"]!.GetValue<int>())break;
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
                    using(var s=scopes.CreateScope())
                    {
                        var result=await s.ServiceProvider.GetRequiredService<IProjectRepository>().IngestArticleAsync(brandId,article);
                        if(result.Duplicate){dup++;foreach(var m in media){var path=BrandPolicy.LocalMedia(env.ContentRootPath,m?["url"]?.ToString());if(path is not null)File.Delete(path);}}
                        else{count++;perAccount++;savedIds.Add(result.Record.Id);}
                    }
                    await State(id,"running",$"Đã lưu {count}/{max} bài; bỏ qua {dup} bài trùng.",count:count,duplicates:dup);
                }
                if(count<max){await page.Mouse.WheelAsync(0,850);await Delay(id,Random.Shared.Next(1500,3001),ct);}
            }
            if(count<max)await Delay(id,Random.Shared.Next(job["accountRestMinMs"]!.GetValue<int>(),job["accountRestMaxMs"]!.GetValue<int>()+1),ct);
        }
        await State(id,"running",$"Thu thập được {count}/{max} bài. Đang lần lượt đánh giá Clef.",count:count,duplicates:dup);
        var evaluated=0;
        foreach(var articleId in savedIds)
        {
            if(await Cancelled(id))throw new OperationCanceledException();
            using var s=scopes.CreateScope();var result=await s.ServiceProvider.GetRequiredService<IContentService>().EvaluateArticle(articleId);if(result.Status==200)evaluated++;
            await State(id,"running",$"Clef đánh giá {evaluated}/{savedIds.Count} bài; lỗi AI giữ bài ở trạng thái chờ.",count:count,duplicates:dup);
        }
        await State(id,count==max?"completed":"paused",$"Đã lưu {count}/{max} bài, bỏ qua {dup} bài trùng; Clef đã đánh giá {evaluated}. Không đăng Facebook.",count==max?null:"SOURCE_EXHAUSTED",count,dup);
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
