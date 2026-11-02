using Microsoft.Playwright;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace SammiCrawl.Backend.Services;

public sealed class FacebookCrawlWorker(IServiceScopeFactory scopes, IDataProtectionProvider protection, IWebHostEnvironment env, ILogger<FacebookCrawlWorker> log, CrawlGlobalLock? globalLock = null, CrawlWorkerIdentity? identity = null) : BackgroundService
{
    readonly Channel<(Guid Id, string Token)> queue = Channel.CreateUnbounded<(Guid Id, string Token)>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    readonly HttpClient mediaHttp = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan };
    public override void Dispose() { mediaHttp.Dispose(); base.Dispose(); }
    string? activeToken;
    CrawlGlobalLease? activeLease;
    public string OwnerId => identity?.Id ?? "fixture";
    public bool Enqueue(Guid id, string token) => queue.Writer.TryWrite((id, token));
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if(globalLock is null)throw new InvalidOperationException("Thiếu khóa cào toàn cụm.");
        // Only our persisted waiting entries are paused on restart. Never rewrite another node's live tasks.
        using(var scope=scopes.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            foreach(var d in await db.ListDocumentsAsync("tasks"))
            {
                var j=JsonNode.Parse(d.Json)!.AsObject();
                if(j["workerOwnerId"]?.ToString()==OwnerId&&j["status"]?.ToString()=="pending")
                {
                    j["status"]="paused";j["pauseAcknowledged"]=true;j["errorReason"]="Máy phụ trách khởi động lại; bấm Tiếp tục để xếp hàng lại.";
                    await db.TryUpdateTaskAsync(d.Id,d.Json,j.ToJsonString(),$"Tạm dừng lượt chờ sau restart {d.Id}");
                }
            }
        }
        using var recoveryStop=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var recovery=RecoveryLoop(recoveryStop.Token);
        try
        {
        await foreach(var item in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var lease=await WaitForGlobalTurn(item.Id,item.Token,stoppingToken);
                if(lease is null)continue;
                activeLease=lease;
                using var runStop=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken,lease.LostToken);
                var heartbeat=Heartbeat(item.Id,item.Token,runStop.Token);
                try{await Run(item.Id,item.Token,runStop.Token);}
                catch(Exception) when(runStop.IsCancellationRequested)
                {await State(item.Id,"paused","Worker dừng hoặc mất khóa DB; không tự cào lại.","CRAWL_LOCK_LOST");}
                catch(OperationCanceledException){await State(item.Id,"paused","Tác vụ đã dừng.");}
                catch(Exception ex){log.LogWarning("Crawl {JobId} failed: {Type}",item.Id,ex.GetType().Name);await State(item.Id,"failed","Không hoàn tất thu thập; xem số bài đã lưu.","CRAWL_FAILED");}
                finally{runStop.Cancel();await heartbeat;}
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){log.LogWarning("Crawl coordination {JobId}: {Type}",item.Id,ex.GetType().Name);}
            finally{activeToken=null;activeLease=null;}
        }
        }
        finally{recoveryStop.Cancel();await recovery;}
    }
    async Task RecoveryLoop(CancellationToken ct)
    {
        try
        {
            while(true)
            {
                await Task.Delay(TimeSpan.FromSeconds(10),ct);
                try{await using var lease=await globalLock!.TryAcquire(ct);if(lease is not null)await InspectActiveTasks(lease,ct);}
                catch(OperationCanceledException) when(ct.IsCancellationRequested){return;}
                catch(Exception ex){log.LogWarning("Crawl recovery unavailable: {Type}",ex.GetType().Name);}
            }
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){}
    }
    async Task<bool> InspectActiveTasks(CrawlGlobalLease lease,CancellationToken ct)
    {
        var blocked=false;var now=await lease.DatabaseTime(ct);
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        foreach(var d in await db.ListDocumentsAsync("tasks"))
        {
            var j=JsonNode.Parse(d.Json)!.AsObject();if(!CrawlOrphanPolicy.Blocks(j))continue;
            if(CrawlOrphanPolicy.Recoverable(j,now))
            {
                j["status"]="paused";j["pauseAcknowledged"]=true;j["errorCode"]="CRAWL_WORKER_LOST";j["errorReason"]="Worker mất heartbeat; đã giải phóng lượt, không tự chạy lại. Kiểm tra rồi bấm Tiếp tục.";
                lease.ThrowIfLost();
                if(!await db.TryUpdateTaskAsync(d.Id,d.Json,j.ToJsonString(),$"Worker bỏ dở tác vụ {d.Id}"))blocked=true;
            }
            else blocked=true;
        }
        lease.ThrowIfLost();return blocked;
    }
    async Task<CrawlGlobalLease?> WaitForGlobalTurn(Guid id,string token,CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            using(var scope=scopes.CreateScope())
            {
                var d=await scope.ServiceProvider.GetRequiredService<IProjectRepository>().FindDocumentAsync(id,"tasks");
                if(d is null||!CrawlResumeState.CanClaim(JsonNode.Parse(d.Json)!.AsObject(),token))return null;
            }
            CrawlGlobalLease? lease=null;
            try
            {
                lease=await globalLock!.TryAcquire(ct);
                if(lease is not null)
                {
                    var blocked=await InspectActiveTasks(lease,ct);
                    if(!blocked)return lease;
                }
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){if(lease is not null)await lease.DisposeAsync();throw;}
            catch(Exception ex){log.LogWarning("Global crawl lock unavailable: {Type}",ex.GetType().Name);}
            if(lease is not null)await lease.DisposeAsync();
            await Task.Delay(TimeSpan.FromSeconds(2),ct);
        }
        return null;
    }
    async Task Heartbeat(Guid id,string token,CancellationToken ct)
    {
        try
        {
            while(true)
            {
                await Task.Delay(TimeSpan.FromSeconds(5),ct);
                try
                {
                    activeLease?.ThrowIfLost();
                    using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IProjectRepository>();var d=await db.FindDocumentAsync(id,"tasks");if(d is null)return;
                    var j=JsonNode.Parse(d.Json)!.AsObject();if(j["executionToken"]?.ToString()!=token||j["status"]?.ToString() is not ("running" or "paused"))return;
                    j["workerHeartbeatAt"]=(await activeLease!.DatabaseTime(ct)).ToString("O");await db.TryUpdateTaskAsync(id,d.Json,j.ToJsonString(),"");
                }
                catch(OperationCanceledException){return;}
                catch(Exception ex){log.LogWarning("Crawl heartbeat failed: {Type}",ex.GetType().Name);}
            }
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){}
    }
    async Task State(Guid id,string status,string message,string? code=null,int? count=null,int? duplicates=null,CrawlEvaluationState? evaluation=null)
    {
        if(status=="running")activeLease?.ThrowIfLost();
        for(var attempt=0;attempt<4;attempt++)
        {
            using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IProjectRepository>();var d=await db.FindDocumentAsync(id,"tasks");if(d is null)return;
            var j=JsonNode.Parse(d.Json)!.AsObject();
            if(activeToken is not null&&j["executionToken"]?.ToString()!=activeToken){if(status=="running")throw new OperationCanceledException();return;}
            if(j["status"]?.ToString()=="paused"&&status=="running")throw new OperationCanceledException();
            j["status"]=status;j["errorReason"]=message;j["errorCode"]=code;
            if(status is "paused" or "failed" or "completed"){j["pauseAcknowledged"]=true;}
            evaluation?.WriteTo(j);if(count.HasValue)j["crawledPosts"]=count.Value;
            var raw=j["crawledPosts"]?.GetValue<int>()??0;var rejected=j["clefRejectedPosts"]?.GetValue<int>()??0;
            j["retainedPosts"]=Math.Max(0,raw-rejected);j["progress"]=CrawlEvaluationState.Progress(status,Math.Max(0,raw-rejected),j["maxPosts"]?.GetValue<int>()??1,Math.Max(0,(j["clefEvaluatedPosts"]?.GetValue<int>()??0)-rejected));
            if(duplicates.HasValue)j["duplicatesSkipped"]=duplicates.Value;
            if(await db.TryUpdateTaskAsync(id,d.Json,j.ToJsonString(),$"Cào {id}: {message}"))return;
        }
        throw new OperationCanceledException();
    }
    async Task<bool> Cancelled(Guid id)
    {using var s=scopes.CreateScope();var d=await s.ServiceProvider.GetRequiredService<IProjectRepository>().FindDocumentAsync(id,"tasks");var j=d is null?null:JsonNode.Parse(d.Json);return activeLease?.LostToken.IsCancellationRequested==true||j is null||j["status"]?.ToString()=="paused"||(activeToken is not null&&j["executionToken"]?.ToString()!=activeToken);}
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
    async Task<bool> PrepareSources(Guid id,JsonObject job,CancellationToken ct)
    {
        if(job["autoDiscover"]?.GetValue<bool>()!=true || job["discoveryPrepared"]?.GetValue<bool>()==true)return true;
        await State(id,"running","AI đang chọn từ khóa tìm nhóm theo hồ sơ thương hiệu; chưa truy cập Facebook.");
        using var scope=scopes.CreateScope();var ai=scope.ServiceProvider.GetRequiredService<AiRunner>();
        string[] keywords;
        try
        {
            await ai.AcquireAsync(ct);
            try { keywords=SourceDiscovery.Parse(await ai.Rewrite(new RewriteRequest(job["discoveryProfile"]!.ToJsonString(),Mode:"source-keywords",Temperature:0.2))); }
            finally { ai.Release(); }
        }
        catch(OperationCanceledException){throw;}
        catch(Exception ex)
        {
            log.LogWarning("Source planning {JobId}: {Type}",id,ex.GetType().Name);
            await State(id,"paused","AI chưa tạo được từ khóa nguồn hợp lệ. Kiểm tra Bonsai/gateway rồi bấm Tiếp tục; chưa truy cập Facebook.","SOURCE_AI_FAILED");return false;
        }
        if(await Cancelled(id))throw new OperationCanceledException();
        var db=scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        for(var attempt=0;attempt<8;attempt++)
        {
            var row=await db.FindDocumentAsync(id,"tasks");if(row is null)throw new OperationCanceledException();
            var current=JsonNode.Parse(row.Json)!.AsObject();
            if(current["status"]?.ToString()!="running"||current["executionToken"]?.ToString()!=activeToken)throw new OperationCanceledException();
            current["groupKeywords"]=string.Join("\n",keywords);current["discoveryPrepared"]=true;
            current["url"]="https://www.facebook.com/search/groups/?q="+Uri.EscapeDataString(keywords[0]);
            activeLease?.ThrowIfLost();
            if(await db.TryUpdateTaskAsync(id,row.Json,current.ToJsonString(),$"AI đã chọn từ khóa nguồn cho {id}"))
            {job["groupKeywords"]=current["groupKeywords"]!.DeepClone();job["url"]=current["url"]!.DeepClone();job["discoveryPrepared"]=true;return true;}
        }
        throw new OperationCanceledException();
    }
    async Task Run(Guid id,string token,CancellationToken ct)
    {
        JsonObject job;List<Guid> accountIds;CrawlResumeState saved;
        using(var s=scopes.CreateScope())
        {
            var db=s.ServiceProvider.GetRequiredService<IProjectRepository>();var d=await db.FindDocumentAsync(id,"tasks");if(d is null)return;
            job=JsonNode.Parse(d.Json)!.AsObject();
            if(!CrawlResumeState.CanClaim(job,token))return;
            activeLease?.ThrowIfLost();job["status"]="running";job["pauseAcknowledged"]=false;job["crawlLockProtocol"]=1;job["workerOwnerId"]=OwnerId;job["workerHeartbeatAt"]=(await activeLease!.DatabaseTime(ct)).ToString("O");
            if(!await db.TryUpdateTaskAsync(id,d.Json,job.ToJsonString(),$"Worker nhận tác vụ {id}"))return;
            activeToken=token;accountIds=job["crawlAccountIds"]!.AsArray().Select(n=>Guid.Parse(n!.ToString())).ToList();
            saved=new CrawlResumeState(job,(await db.ListDocumentsAsync("articles")).Select(d=>JsonNode.Parse(d.Json)!.AsObject()));
        }
        if(!await PrepareSources(id,job,ct))return;
        var target=job["url"]!.ToString();var searchGroups=job["sourceMode"]?.ToString()=="group-search"||job["autoDiscover"]?.GetValue<bool>()==true;var groupCounts=saved.BySource;var visitedGroups=new HashSet<string>(groupCounts.Keys);var brandId=Guid.Parse(job["brandId"]!.ToString());var max=job["maxPosts"]!.GetValue<int>();var count=saved.Captured;var dup=saved.Duplicates;var evaluation=saved.Evaluation;var seen=saved.Seen;
        await State(id,"running","Bắt đầu/tiếp tục thu thập, giữ bài đã lưu và kiểm tra trùng.",count:count,duplicates:dup,evaluation:evaluation);
        foreach(var articleId in saved.PendingEvaluation)
        {
            if(await Cancelled(id))throw new OperationCanceledException();
            using var s=scopes.CreateScope();var assessed=await s.ServiceProvider.GetRequiredService<IContentService>().EvaluateArticle(articleId);
            if(!evaluation.Record(assessed)){await State(id,"paused","Bài đã lưu còn chờ Clef; chưa tiếp tục thu thập.","CLEF_EVALUATION_INCOMPLETE",count,dup,evaluation);return;}
            await State(id,"running","Đã đánh giá tiếp bài đã lưu.",count:count,duplicates:dup,evaluation:evaluation);
        }
        if(evaluation.Retained(count)>=max){await State(id,evaluation.Evaluated>=count?"completed":"paused","Đã kiểm tra số bài và đánh giá đã lưu.",evaluation.Evaluated>=count?null:"CLEF_EVALUATION_INCOMPLETE",count,dup,evaluation);return;}
        using var pw=await Playwright.CreateAsync();await using var browser=await pw.Chromium.LaunchAsync(new(){Headless=true,Timeout=30000});using var closeOnStop=ct.Register(()=>{_ = CloseBrowser(browser);});
        foreach(var accountId in accountIds)
        {
            if(evaluation.Retained(count)>=max)break;
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
            var targets=searchGroups ? await DiscoverGroups(page,job,id,accountId,count,dup,ct) : job["sourceMode"]?.ToString() is "brand-links" or "ai-auto" ? job["sourceUrls"]!.AsArray().Select(u => new JsonObject { ["url"]=u!.ToString() }).ToList() : new List<JsonObject> { new() { ["url"]=target } };
            if(targets is null)return;
            var perAccount=saved.ByAccount.GetValueOrDefault(accountId.ToString());
            foreach(var group in targets)
            {
            if(evaluation.Retained(count)>=max||perAccount>=job["accountPostLimit"]!.GetValue<int>())break;
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
            for(var scroll=0;scroll<job["maxScrolls"]!.GetValue<int>()&&evaluation.Retained(count)<max&&perAccount<job["accountPostLimit"]!.GetValue<int>()&&groupCounts.GetValueOrDefault(target)<groupLimit;scroll++)
            {
                if(await Cancelled(id))throw new OperationCanceledException();
                if(!await CheckSession(page,id,accountId,count,dup))return;
                // Expand only visible post text; never click reactions, shares or join controls.
                var more=page.GetByRole(AriaRole.Button,new(){Name="Xem thêm",Exact=true});
                for(var k=0;k<Math.Min(await more.CountAsync(),12);k++){try{if(await more.Nth(k).IsVisibleAsync())await more.Nth(k).ClickAsync(new(){Timeout=1500});}catch{/* text may have rerendered */}}
                var metadataCapturedAt = DateTimeOffset.UtcNow;
                await page.EvaluateAsync(FacebookPostMedia.WaitVisibleImages);
                var json=await page.EvaluateAsync<string>("(() => { const metadata = " + FacebookPostMetadata.Script + "; const extractMedia = " + FacebookPostMedia.Script + "; return (" + """
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
                    return {sourceUrl:source,content:text,author:h.innerText,metadata:metadata(n,source),...extractMedia(n)};
                  }return null;
                }).filter(Boolean))
                """ + ")(); })()");
                var candidates=JsonNode.Parse(json)!.AsArray();if(candidates.Count==0&&scroll%5==0)await State(id,"running",$"Chưa nhận diện bài mới ở lần cuộn {scroll+1}; tiếp tục trong giới hạn đã đặt.",count:count,duplicates:dup);
                foreach(var candidate in candidates)
                {
                    if(evaluation.Retained(count)>=max||perAccount>=job["accountPostLimit"]!.GetValue<int>()||groupCounts.GetValueOrDefault(target)>=groupLimit)break;
                    var source=candidate!["sourceUrl"]!.ToString();var sourceKey=ArticleDeduplication.SourceKey(source);if(sourceKey is null||!seen.Add(sourceKey))continue;
                    // Delay between processed posts, including duplicates; this is not anti-detection evasion.
                    if(seen.Count>1)await Delay(id,Random.Shared.Next(job["delayMinMs"]!.GetValue<int>(),job["delayMaxMs"]!.GetValue<int>()+1),ct);
                    var keywords=(job["keywords"]?.ToString()??"").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                    if(keywords.Length>0&&!keywords.Any(k=>candidate["content"]!.ToString().Contains(k,StringComparison.OrdinalIgnoreCase)))continue;
                    var media=new JsonArray();var warnings=new JsonArray();if(candidate["unresolvedImages"]?.GetValue<int>()>0)warnings.Add("Có ảnh nguồn tải chậm hoặc chưa đọc được link; media có thể thiếu.");if(candidate["images"]!.AsArray().Count>0)warnings.Add("Ảnh là bản Facebook cung cấp trên feed; chưa xác minh độ phân giải gốc.");
                    foreach(var src in candidate["images"]!.AsArray().Select(n=>n!.ToString()).Distinct().Take(12))
                    {var m=await Download(context,src,"image",id,ct);
                        if(m is null&&candidate["imageAlternatives"]?[src] is JsonArray alternatives)
                            foreach(var alt in alternatives.Take(2)){if(await Cancelled(id))throw new OperationCanceledException();m=await Download(context,alt!.ToString(),"image",id,ct);if(m is not null)break;}
                        if(m is not null)media.Add(m);else warnings.Add("Không tải được một ảnh của bài nguồn.");}
                    foreach(var src in candidate["videos"]!.AsArray().Select(n=>n!.ToString()).Distinct().Take(4))
                    {var m=await Download(context,src,"video",id,ct);if(m is not null)media.Add(m);else warnings.Add("Video chưa tải được file gốc; không coi media là đầy đủ.");}
                    var article=new JsonObject{["content"]=candidate["content"]!.ToString(),["title"]=candidate["content"]!.ToString()[..Math.Min(100,candidate["content"]!.ToString().Length)],["sourceUrl"]=source,["sourceAccountId"]=accountId.ToString(),["crawlTaskId"]=id.ToString(),["crawlSourceUrl"]=target,["media"]=media,["mediaWarnings"]=warnings,["aiStatus"]="pending",["sourceAuthor"]=candidate["author"]!.ToString(),["crawlTopicDescription"]=job["topicDescription"]?.ToString()};
                    FacebookPostMetadata.Apply(article, candidate["metadata"], metadataCapturedAt);
                    if(sourceGroupId.HasValue) { article["sourceGroupId"]=sourceGroupId.Value.ToString(); article["sourceGroupName"]=group["name"]!.ToString(); article["sourceGroupUrl"]=target; article["channelEvaluationRequired"]=true; }
                    if(await Cancelled(id))throw new OperationCanceledException();
                    using(var s=scopes.CreateScope())
                    {
                        var result=await s.ServiceProvider.GetRequiredService<IProjectRepository>().IngestArticleAsync(brandId,article);
                        if(result.Duplicate){dup++;foreach(var m in media){var path=BrandPolicy.LocalMedia(env.ContentRootPath,m?["url"]?.ToString());if(path is not null)File.Delete(path);}}
                        else
                        {
                            count++;perAccount++;groupCounts[target]=groupCounts.GetValueOrDefault(target)+1;
                            await State(id,"running",$"Đã thu thập {evaluation.Retained(count)}/{max} bài giữ lại (đã quét {count}); đang đánh giá Clef bài vừa thu thập.",count:count,duplicates:dup,evaluation:evaluation);
                            if(await Cancelled(id))throw new OperationCanceledException();
                            var assessed=await s.ServiceProvider.GetRequiredService<IContentService>().EvaluateArticle(result.Record.Id);
                            if(await Cancelled(id))throw new OperationCanceledException();
                            if(!evaluation.Record(assessed))
                            {
                                var reason=(assessed.Value as JsonObject)?["error"]?.ToString()
                                    ?? (assessed.Value is null ? null : System.Text.Json.JsonSerializer.SerializeToNode(assessed.Value)?["error"]?.ToString())
                                    ?? "Clef chưa trả kết quả hợp lệ.";
                                await State(id,"paused",$"Đã thu thập {evaluation.Retained(count)}/{max} bài giữ lại (đã quét {count}); Clef đánh giá {evaluation.Evaluated}/{count}. {reason} Bài chưa đánh giá được giữ chờ xử lý; tác vụ chưa hoàn thành.","CLEF_EVALUATION_INCOMPLETE",count,dup,evaluation);
                                return;
                            }
                        }
                    }
                    await State(id,"running",$"Đã thu thập {evaluation.Retained(count)}/{max} bài giữ lại (đã quét {count}); Clef {evaluation.Evaluated}/{count}: phù hợp {evaluation.Recommended}, cần xem lại {evaluation.Review}, đã loại {evaluation.Rejected}; bỏ qua {dup} bài trùng.",count:count,duplicates:dup,evaluation:evaluation);
                }
                if(evaluation.Retained(count)<max){await page.Mouse.WheelAsync(0,850);await Delay(id,Random.Shared.Next(1500,3001),ct);}
            }
            }
            if(evaluation.Retained(count)<max)await Delay(id,Random.Shared.Next(job["accountRestMinMs"]!.GetValue<int>(),job["accountRestMaxMs"]!.GetValue<int>()+1),ct);
        }
        var complete=evaluation.TargetReached(count,max);
        var noGroups=searchGroups&&visitedGroups.Count==0;
        var summary=$"Đã thu thập {evaluation.Retained(count)}/{max} bài giữ lại (đã quét {count}); Clef đánh giá {evaluation.Evaluated}/{count}: phù hợp {evaluation.Recommended}, cần xem lại {evaluation.Review}, đã loại {evaluation.Rejected}; bỏ qua {dup} bài trùng. Không đăng Facebook.";
        if(noGroups)summary="Không tìm được nhóm nguồn từ các từ khóa đã nhập. Đổi từ khóa tên nhóm hoặc dùng link nguồn cụ thể trong thương hiệu. Chưa có bài nào gửi Clef. "+summary;
        await State(id,complete?"completed":"paused",summary,complete?null:noGroups?"NO_SOURCE_GROUPS":"SOURCE_EXHAUSTED",count,dup,evaluation);
    }
    static async Task CloseBrowser(IBrowser browser){try{await browser.CloseAsync();}catch{/* Cleanup must not hide the cancellation reason. */}}
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
