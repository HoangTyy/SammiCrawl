using Microsoft.AspNetCore.DataProtection;
using Microsoft.Playwright;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public sealed class FacebookShareWorker(IServiceScopeFactory scopes,CrawlWorkerIdentity identity,CrawlGlobalLock globalLock,IDataProtectionProvider protection,FacebookShareBrowser adapter,ILogger<FacebookShareWorker> log):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Never replay an external write after restart, including a previously waiting request.
        using(var scope=scopes.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            foreach(var doc in await db.ListDocumentsAsync("drafts"))
            {
                var data=JsonNode.Parse(doc.Json)!.AsObject();
                if(data["branch"]?.ToString()!="share"||data["workerOwnerId"]?.ToString()!=identity.Id)continue;
                if(data["status"]?.ToString() is not ("share_queued" or "share_running" or "share_sending"))continue;
                var attempted=data["status"]?.ToString()=="share_sending";
                data["status"]=attempted?"share_unknown":"share_cancelled";data["shareCode"]="SHARE_WORKER_RESTARTED";data["shareMessage"]="Máy chia sẻ khởi động lại; không tự gửi lại. Kiểm tra nhóm nếu đã bấm gửi.";
                await db.TryUpdateDraftAsync(doc.Id,doc.Json,data.ToJsonString(),$"Dừng lượt chia sẻ sau restart {doc.Id}");
            }
        }
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IProjectRepository>();
                var next=(await db.ListDocumentsAsync("drafts")).Where(d=>{var j=JsonNode.Parse(d.Json)!;return j["status"]?.ToString()=="share_queued"&&j["workerOwnerId"]?.ToString()==identity.Id;}).OrderBy(d=>d.CreatedAt).FirstOrDefault();
                if(next is not null)
                {
                    // Reuse the cluster-wide Facebook lock: sharing also waits for an active crawl.
                    await using var lease=await globalLock.TryAcquire(stoppingToken);
                    if(lease is not null)
                    {
                        using var stop=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken,lease.LostToken);stop.CancelAfter(TimeSpan.FromMinutes(3));
                        await Run(next.Id,stop.Token);
                    }
                }
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){log.LogWarning("Share worker coordination failed: {Type}",ex.GetType().Name);}
            try{await Task.Delay(1500,stoppingToken);}catch(OperationCanceledException){break;}
        }
    }
    async Task Run(Guid id,CancellationToken ct)
    {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var doc=await db.FindDocumentAsync(id,"drafts");if(doc is null)return;
        var job=JsonNode.Parse(doc.Json)!.AsObject();if(job["status"]?.ToString()!="share_queued")return;
        job["status"]="share_running";job["updatedAt"]=DateTimeOffset.UtcNow.ToString("O");
        if(!await db.TryUpdateDraftAsync(id,doc.Json,job.ToJsonString(),$"Bắt đầu chia sẻ {id}"))return;
        var attempted=false;FacebookShareResult result;
        try
        {
            var account=await db.FindDocumentAsync(Guid.Parse(job["publishAccountId"]!.ToString()),"accounts");
            var a=account is null?null:JsonNode.Parse(account.Json);
            if(!FacebookShareService.Eligible(account,a))throw new InvalidOperationException("SHARE_ACCOUNT_UNAVAILABLE");
            var originalSecret=account!.ProtectedSecret;
            var raw=protection.CreateProtector("FacebookSession.v1").Unprotect(originalSecret!);
            var cookies=Cookies(raw);
            var uid=cookies.SingleOrDefault(c=>c.Name=="c_user")?.Value;
            if(uid!=a!["uid"]?.ToString()||string.IsNullOrEmpty(uid)||!cookies.Any(c=>c.Name=="xs"&&!string.IsNullOrEmpty(c.Value)))throw new InvalidOperationException("SHARE_IDENTITY_MISMATCH");
            using var pw=await Playwright.CreateAsync();await using var browser=await pw.Chromium.LaunchAsync(new(){Headless=true,Timeout=30000});
            using var close=ct.Register(()=>{_=browser.CloseAsync();});
            await using var context=await browser.NewContextAsync(new(){Locale="en-US",ViewportSize=new(){Width=1365,Height=900}});
            await context.AddCookiesAsync(cookies);var page=await context.NewPageAsync();page.SetDefaultTimeout(10000);
            result=await adapter.Share(page,job["sourceUrl"]!.ToString(),job["channelSnapshot"]!["url"]!.ToString(),async()=>
            {
                ct.ThrowIfCancellationRequested();
                using var checkScope=scopes.CreateScope();var fresh=checkScope.ServiceProvider.GetRequiredService<IProjectRepository>();
                var current=await fresh.FindDocumentAsync(id,"drafts");var data=current is null?null:JsonNode.Parse(current.Json)!.AsObject();
                if(data?["status"]?.ToString()!="share_running")return false;
                var publisher=await fresh.FindDocumentAsync(account.Id,"accounts");var pj=publisher is null?null:JsonNode.Parse(publisher.Json);
                var brand=await fresh.FindDocumentAsync(Guid.Parse(job["brandId"]!.ToString()),"brands");var profile=brand is null?null:JsonNode.Parse(brand.Json);
                var channel=profile is null||profile["deletedAt"] is not null?null:BrandPolicy.SelectChannel(profile,job["channelId"]?.ToString());
                if(!FacebookShareService.Eligible(publisher,pj)||publisher!.ProtectedSecret!=originalSecret||channel?["type"]?.ToString()!="group"||channel?["url"]?.ToString()!=job["channelSnapshot"]?["url"]?.ToString())return false;
                data!["status"]="share_sending";data["shareAttemptedAt"]=DateTimeOffset.UtcNow.ToString("O");
                if(!await fresh.TryUpdateDraftAsync(id,current!.Json,data.ToJsonString(),$"Chuẩn bị gửi chia sẻ {id}"))return false;
                attempted=true;return true;
            });
            if(result.Code is "LOGIN_REQUIRED" or "CHECKPOINT" or "ACCOUNT_LOCKED" or "RATE_LIMITED")
            {
                using var errorScope=scopes.CreateScope();var errorDb=errorScope.ServiceProvider.GetRequiredService<IProjectRepository>();var row=await errorDb.FindDocumentAsync(account.Id,"accounts");
                if(row is not null&&row.ProtectedSecret==originalSecret){var data=JsonNode.Parse(row.Json)!.AsObject();FacebookAccountHealth.Apply(data,FacebookAccountHealth.Runtime(result.Code),"share_worker");row.Json=data.ToJsonString();await errorDb.SaveChangesAsync();}
            }
        }
        catch(Exception ex)
        {
            log.LogWarning("Share {JobId}: {Type}",id,ex.GetType().Name);
            result=new(attempted?"share_unknown":"share_failed",attempted?"SHARE_RESULT_UNKNOWN":"SHARE_NOT_SENT",attempted?"Chưa xác định kết quả gửi. Kiểm tra nhóm; không tự gửi lại.":"Chưa gửi bài: kiểm tra phiên tài khoản, quyền nhóm hoặc giao diện chia sẻ Facebook.");
        }
        using var finalScope=scopes.CreateScope();var finalDb=finalScope.ServiceProvider.GetRequiredService<IProjectRepository>();var finalDoc=await finalDb.FindDocumentAsync(id,"drafts");if(finalDoc is null)return;
        var final=JsonNode.Parse(finalDoc.Json)!.AsObject();if(final["status"]?.ToString() is not ("share_running" or "share_sending"))return;
        final["status"]=result.Status;final["shareCode"]=result.Code;final["shareMessage"]=result.Message;final["updatedAt"]=DateTimeOffset.UtcNow.ToString("O");
        if(result.Status=="published"){final["publishedUrl"]=result.Url;final["publishedAt"]=DateTimeOffset.UtcNow.ToString("O");final["publicationEvidence"]="facebook_confirmation";}
        await finalDb.TryUpdateDraftAsync(id,finalDoc.Json,final.ToJsonString(),$"Chia sẻ {id}: {result.Code}");
    }
    static List<Cookie> Cookies(string raw)
    {
        var result=new List<Cookie>();
        if(raw.TrimStart().StartsWith('[')||raw.TrimStart().StartsWith('{'))
        {
            var node=JsonNode.Parse(raw);foreach(var c in node as JsonArray??node?["cookies"]?.AsArray()??new JsonArray())if(c?["name"] is not null)result.Add(new(){Name=c["name"]!.ToString(),Value=c["value"]!.ToString(),Domain=".facebook.com",Path="/",Secure=true});
        }
        else foreach(var part in raw.Split(';')){var pair=part.Trim().Split('=',2);if(pair.Length==2)result.Add(new(){Name=pair[0],Value=pair[1],Domain=".facebook.com",Path="/",Secure=true});}
        return result.GroupBy(c=>c.Name).Select(g=>g.Last()).ToList();
    }
}
