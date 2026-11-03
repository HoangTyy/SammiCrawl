using Microsoft.Playwright;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json.Nodes;
namespace SammiCrawl.Backend.Services;
public sealed class FacebookScopeReader(IDataProtectionProvider protection)
{
 List<Cookie> Cookies(string secret)
 {
  var raw=protection.CreateProtector("FacebookSession.v1").Unprotect(secret);var cookies=new List<Cookie>();
  if(raw.TrimStart().StartsWith('[')||raw.TrimStart().StartsWith('{'))
  {var n=JsonNode.Parse(raw)!;foreach(var c in n as JsonArray??n["cookies"]!.AsArray())if(c?["name"] is not null)cookies.Add(new(){Name=c["name"]!.ToString(),Value=c["value"]!.ToString(),Domain=".facebook.com",Path="/",Secure=true});}
  else foreach(var part in raw.Split(';')){var pair=part.Trim().Split('=',2);if(pair.Length==2)cookies.Add(new(){Name=pair[0],Value=pair[1],Domain=".facebook.com",Path="/",Secure=true});}
  return cookies;
 }
 public async Task<JsonObject> Search(string secret,string keyword,CancellationToken ct)
 {
  using var pw=await Playwright.CreateAsync();await using var browser=await pw.Chromium.LaunchAsync(new(){Headless=true});await using var context=await browser.NewContextAsync(new(){Locale="vi-VN"});await context.AddCookiesAsync(Cookies(secret));var page=await context.NewPageAsync();
  using var cancel=ct.Register(()=>{_ = browser.CloseAsync();});
  var response=await page.GotoAsync("https://www.facebook.com/search/groups/?q="+Uri.EscapeDataString(keyword),new(){WaitUntil=WaitUntilState.DOMContentLoaded,Timeout=45000});
  var result=new JsonObject{["query"]=keyword,["readOnly"]=true,["httpStatus"]=response?.Status,["finalPath"]=new Uri(page.Url).AbsolutePath};var groups=new JsonArray();
  for(var poll=0;poll<12;poll++)
  {
   await Task.Delay(1000,ct);
   if(page.Url.Contains("/login")||page.Url.Contains("checkpoint")||await page.Locator("input[name=email]").CountAsync()>0)throw new InvalidOperationException("Phiên Facebook cần đăng nhập/xác minh.");
   var body=(await page.Locator("body").InnerTextAsync()).ToLowerInvariant();
   if(body.Contains("temporarily blocked")||body.Contains("you're going too fast"))throw new InvalidOperationException("Facebook đang giới hạn thao tác.");
   var raw=await page.EvaluateAsync<string>("""
    () => JSON.stringify(Array.from(document.querySelectorAll('a[href*="/groups/"]')).map(a=>({name:(a.innerText||a.getAttribute('aria-label')||'').trim(),url:a.href})).filter(a=>a.name.length>1))
    """);
   var links=JsonNode.Parse(raw)!.AsArray();var seen=new HashSet<string>();groups=new JsonArray();
   foreach(var item in links){var url=SourceGroupPolicy.CanonicalUrl(item?["url"]?.ToString());if(url is null||!seen.Add(url))continue;groups.Add(new JsonObject{["url"]=url,["name"]=item?["name"]?.ToString(),["matches"]=SourceGroupPolicy.MatchesSearch(item?["name"]?.ToString()??"",keyword)});if(groups.Count>=30)break;}
   result["namedLinks"]=links.Count;result["polls"]=poll+1;
   if(groups.Count>0)break;
  }
  result["groups"]=groups;result["observedAt"]=DateTimeOffset.UtcNow.ToString("O");
  if(groups.Count==0)
  {
   var text=(await page.Locator("body").InnerTextAsync()).ToLowerInvariant();
   result["bodyLength"]=text.Length;
   result["notices"]=new JsonArray(new[]{"không có kết quả","không tìm thấy","nội dung này hiện không hiển thị","không thể xử lý","tạm thời bị chặn","xác nhận danh tính","đăng nhập","no results","content isn't available","something went wrong","log in"}.Where(text.Contains).Select(s=>(JsonNode?)JsonValue.Create(s)).ToArray());
   // Private diagnostic artifact only; never served by the web application.
   await page.ScreenshotAsync(new(){Path="/tmp/sammicrawl-search-probe.png"});
  }
  return result;
 }
 public async Task<JsonArray> Read(string secret,IEnumerable<string> urls,CancellationToken ct)
 {
  using var pw=await Playwright.CreateAsync();await using var browser=await pw.Chromium.LaunchAsync(new(){Headless=true});await using var context=await browser.NewContextAsync(new(){Locale="vi-VN"});await context.AddCookiesAsync(Cookies(secret));var page=await context.NewPageAsync();
  using var cancel=ct.Register(()=>{_ = browser.CloseAsync();});var samples=new JsonArray();var seen=new HashSet<string>();
  foreach(var url in urls.Take(2))
  {
   ct.ThrowIfCancellationRequested();await page.GotoAsync(url,new(){WaitUntil=WaitUntilState.DOMContentLoaded,Timeout=45000});await Task.Delay(2000,ct);
   if(page.Url.Contains("login")||page.Url.Contains("checkpoint")||await page.Locator("input[name=email]").CountAsync()>0)throw new InvalidOperationException("Phiên Facebook cần đăng nhập/xác minh trước khi đọc scope.");
   for(var scroll=0;scroll<6&&samples.Count<10;scroll++)
   {
    var output=await page.EvaluateAsync<string>("""
     () => JSON.stringify(Array.from(document.querySelectorAll('h2')).map(h=>{let n=h;for(let i=0;n&&i<14;i++,n=n.parentElement){if(!n.querySelector('[aria-label^="Thích"],[aria-label^="Like"],[aria-label^="Bày tỏ cảm xúc"]'))continue;const links=Array.from(n.querySelectorAll('a[href]')).map(a=>a.href);let source=links.find(u=>/\/(posts|permalink)\//.test(u)||u.includes('story_fbid='));if(!source){const photo=links.find(u=>u.includes('set=gm.'));const m=photo?.match(/set=gm\.(\d+)/);const g=location.pathname.match(/groups\/([^/]+)/);if(m&&g)source='https://www.facebook.com/groups/'+g[1]+'/posts/'+m[1]+'/';}const message=n.querySelector('[data-ad-preview="message"],[data-ad-comet-preview="message"],[data-ad-rendering-role="story_message"]');let text=message?.innerText||n.innerText; text=text.split('Đã chia sẻ bài viết')[0].split('Tất cả cảm xúc:')[0].trim();if(text.length<30)return null;return {url:source||location.href,permalinkVerified:!!source,text:text.slice(0,650),adminMarkerVisible:/Quản trị viên|Người kiểm duyệt|Admin|Moderator/.test(n.innerText),mediaCount:n.querySelectorAll('img,video').length};}return null;}).filter(Boolean))
    """);
    foreach(var item in JsonNode.Parse(output)!.AsArray())
    {
     var source=ArticleDeduplication.SourceKey(item?["url"]?.ToString()??"");if(source is null)source=ArticleDeduplication.ContentKey(new JsonObject{["content"]=item?["text"]?.ToString()??""});if(source is null||!seen.Add(source))continue;
     var sample=item!.DeepClone().AsObject();if(ArticleDeduplication.SourceKey(sample["url"]?.ToString()??"") is null){sample["url"]=url;sample["permalinkVerified"]=false;}
     var sourceUri=new Uri(sample["url"]!.ToString());
     sample["url"]=sourceUri.AbsolutePath.Contains("/posts/")||sourceUri.AbsolutePath.Contains("/permalink/")?sourceUri.GetLeftPart(UriPartial.Path):sourceUri.GetLeftPart(UriPartial.Path)+"?"+string.Join("&",sourceUri.Query.TrimStart('?').Split('&').Where(p=>p.StartsWith("story_fbid=")||p.StartsWith("id=")));
     sample["channelUrl"]=url;sample["observedInGroup"]=true;sample["manualApprovalVerified"]=false;sample["observedAt"]=DateTimeOffset.UtcNow.ToString("O");samples.Add(sample);if(samples.Count>=10)break;
    }
    await page.Mouse.WheelAsync(0,900);await Task.Delay(1500,ct);
   }
  }
  return samples;
 }
}
