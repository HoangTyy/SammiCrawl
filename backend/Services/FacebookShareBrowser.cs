using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace SammiCrawl.Backend.Services;

public sealed record FacebookShareResult(string Status,string Code,string Message,string? Url=null);

// All side effects stay behind this adapter so tests can replace Facebook with an offline DOM.
public sealed class FacebookShareBrowser
{
    static readonly Regex ShareButton=new("^(Share(?:,.*)?|Chia sẻ(?:,.*)?)$",RegexOptions.IgnoreCase);
    static readonly Regex Group=new("^(Share to a group|Share in a group|Chia sẻ lên nhóm|Chia sẻ trong nhóm|Chia sẻ vào nhóm)$",RegexOptions.IgnoreCase);
    static readonly Regex Submit=new("^(Post|Đăng|Share now|Chia sẻ ngay)$",RegexOptions.IgnoreCase);
    static async Task ClickOne(ILocator locator)
    {
        await locator.First.WaitForAsync(new(){State=WaitForSelectorState.Visible,Timeout=10000});
        if(await locator.CountAsync()!=1)throw new InvalidOperationException("SHARE_UI_AMBIGUOUS");
        await locator.ClickAsync();
    }
    public static async Task<string?> AccountError(IPage page)
    {
        var path=new Uri(page.Url).AbsolutePath;
        if(path.Contains("checkpoint")||path.Contains("two_step_verification"))return "CHECKPOINT";
        if(path.Contains("/login")||await page.Locator("input[name=email]").CountAsync()>0)return "LOGIN_REQUIRED";
        var text=(await page.Locator("body").InnerTextAsync()).ToLowerInvariant();
        if(text.Contains("your account has been locked")||text.Contains("we suspended your account"))return "ACCOUNT_LOCKED";
        if(text.Contains("temporarily blocked")||text.Contains("tạm thời bị chặn"))return "RATE_LIMITED";
        return null;
    }
    // Caller owns context/cookies and cancellation. A submit is attempted only once.
    public async Task<FacebookShareResult> Share(IPage page,string sourceUrl,string groupUrl,Func<Task<bool>> authorizeSubmit)
    {
        if(ArticleDeduplication.SourceKey(sourceUrl) is null)return new("share_failed","SOURCE_NOT_VERIFIED","Link nguồn chưa hỗ trợ xác định bài gốc.");
        var target=SourceGroupPolicy.CanonicalUrl(groupUrl)??throw new InvalidOperationException("INVALID_GROUP");
        await page.GotoAsync(target,new(){WaitUntil=WaitUntilState.DOMContentLoaded,Timeout=30000});
        if(await AccountError(page) is string accountError)return new("share_failed",accountError,"Phiên Facebook cần kiểm tra; chưa chia sẻ.");
        // Read the actual group's title rather than trusting the editable channel label.
        var headings=page.GetByRole(AriaRole.Heading,new(){Level=1});
        await headings.First.WaitForAsync(new(){State=WaitForSelectorState.Visible});
        if(await headings.CountAsync()!=1)return new("share_failed","GROUP_NOT_VERIFIED","Chưa xác định duy nhất nhóm đích.");
        var groupName=(await headings.InnerTextAsync()).Trim();
        var resolved=SourceGroupPolicy.CanonicalUrl(page.Url);
        var configured=target;
        var aliasResolved=resolved is not null&&!Regex.IsMatch(new Uri(target).Segments[2].Trim('/'),@"^\d+$")&&Regex.IsMatch(new Uri(resolved).Segments[2].Trim('/'),@"^\d+$");
        if(string.IsNullOrWhiteSpace(groupName)||resolved is null||(resolved!=target&&!aliasResolved))return new("share_failed","GROUP_NOT_VERIFIED","Facebook chuyển sang nhóm khác; chưa chia sẻ.");
        target=resolved;
        await page.GotoAsync(sourceUrl,new(){WaitUntil=WaitUntilState.DOMContentLoaded,Timeout=30000});
        if(await AccountError(page) is string sourceError)return new("share_failed",sourceError,"Không mở được phiên/bài nguồn.");
        if(ArticleDeduplication.SourceKey(page.Url)!=ArticleDeduplication.SourceKey(sourceUrl))return new("share_failed","SOURCE_NOT_VERIFIED","Chưa xác định đúng bài nguồn.");
        await ClickOne(page.GetByRole(AriaRole.Button,new(){NameRegex=ShareButton}));
        var groupChoice=page.GetByRole(AriaRole.Menuitem,new(){NameRegex=Group}).Or(page.GetByRole(AriaRole.Button,new(){NameRegex=Group}));
        await ClickOne(groupChoice);
        var searchName=new Regex("Search groups|Search for a group|Tìm kiếm nhóm|Tìm nhóm",RegexOptions.IgnoreCase);
        var searchAny=page.GetByRole(AriaRole.Searchbox).Or(page.GetByRole(AriaRole.Textbox,new(){NameRegex=searchName})).Or(page.GetByRole(AriaRole.Combobox,new(){NameRegex=searchName}));
        var dialog=page.GetByRole(AriaRole.Dialog).Filter(new(){Has=searchAny});
        await dialog.First.WaitForAsync(new(){State=WaitForSelectorState.Visible});
        if(await dialog.CountAsync()!=1)return new("share_failed","SHARE_UI_AMBIGUOUS","Có nhiều hộp thoại; chưa chia sẻ.");
        var search=dialog.GetByRole(AriaRole.Searchbox).Or(dialog.GetByRole(AriaRole.Textbox,new(){NameRegex=searchName})).Or(dialog.GetByRole(AriaRole.Combobox,new(){NameRegex=searchName}));
        await search.FillAsync(groupName);
        // Require the selected result to expose the exact configured group URL. Never choose by name alone.
        var links=dialog.GetByRole(AriaRole.Link);
        await links.First.WaitForAsync(new(){State=WaitForSelectorState.Visible});
        var matching=new List<ILocator>();
        for(var i=0;i<await links.CountAsync();i++)
        {
            var href=SourceGroupPolicy.CanonicalUrl(await links.Nth(i).GetAttributeAsync("href")??"");
            if(href==target||href==configured)matching.Add(links.Nth(i));
        }
        if(matching.Count!=1)return new("share_failed","GROUP_NOT_VERIFIED","Không xác minh được link nhóm trong lựa chọn chia sẻ.");
        var row=matching[0].Locator("xpath=ancestor::*[@role='button' or self::button][1]");
        await ClickOne(row);
        dialog=page.GetByRole(AriaRole.Dialog).Filter(new(){Has=page.GetByRole(AriaRole.Button,new(){NameRegex=Submit})});
        await dialog.First.WaitForAsync(new(){State=WaitForSelectorState.Visible});
        if(await dialog.CountAsync()!=1)return new("share_failed","SHARE_UI_AMBIGUOUS","Không xác định được hộp chia sẻ cuối.");
        var hrefs=await dialog.Locator("a[href]").EvaluateAllAsync<string[]>("els=>els.map(e=>e.href)");
        var groupAudience=dialog.GetByRole(AriaRole.Button).Filter(new(){HasTextRegex=new Regex("Public group|Private group|Nhóm công khai|Nhóm riêng tư",RegexOptions.IgnoreCase)});
        if(await groupAudience.CountAsync()!=1||!hrefs.Any(h=>ArticleDeduplication.SourceKey(h)==ArticleDeduplication.SourceKey(sourceUrl)))
            return new("share_failed","SHARE_PREVIEW_NOT_VERIFIED","Chưa thấy đúng nhóm và bài gốc trong khung chia sẻ; không gửi.");
        if(!await authorizeSubmit())return new("share_failed","SHARE_CONTEXT_CHANGED","Kênh/tài khoản/khóa đã thay đổi; chưa chia sẻ.");
        // From here any exception means unknown delivery; the worker must never automatically retry.
        try
        {
            await ClickOne(dialog.GetByRole(AriaRole.Button,new(){NameRegex=Submit}));
            for(var i=0;i<12;i++)
            {
                await page.WaitForTimeoutAsync(500);
                var messages=await page.Locator("[role=alert], [role=status]").AllTextContentsAsync();
                var text=string.Join(" ",messages).ToLowerInvariant();
                if(text.Contains("pending approval")||text.Contains("chờ phê duyệt")||text.Contains("chờ quản trị viên"))return new("share_pending_approval","GROUP_APPROVAL_REQUIRED","Đã gửi chia sẻ, đang chờ quản trị viên nhóm duyệt.");
                if(text.Contains("post shared")||text.Contains("your post has been shared")||text.Contains("đã chia sẻ bài viết"))
                {
                    var urls=await page.Locator("[role=alert] a[href], [role=status] a[href]").EvaluateAllAsync<string[]>("els=>els.map(e=>e.href)");
                    var published=urls.FirstOrDefault(h=>BrandPolicy.FacebookPostUrl(h)&&GroupOfPost(h)==target&&ArticleDeduplication.SourceKey(h)!=ArticleDeduplication.SourceKey(sourceUrl));
                    return published is null?new("share_submitted","SHARE_SENT_NO_PERMALINK","Facebook báo đã chia sẻ; chưa lấy được link bài trong nhóm."):new("published","SHARE_PUBLISHED","Facebook xác nhận chia sẻ và trả link bài trong nhóm.",published);
                }
            }
            return new("share_unknown","SHARE_RESULT_UNKNOWN","Đã bấm gửi nhưng chưa xác định kết quả. Kiểm tra nhóm trước khi thao tác lại.");
        }
        catch{return new("share_unknown","SHARE_RESULT_UNKNOWN","Kết nối gián đoạn khi gửi; không tự chia sẻ lại.");}
    }
    static string? GroupOfPost(string url)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri))return null;
        var match=Regex.Match(uri.AbsolutePath,@"^/groups/([^/]+)/(?:posts|permalink)/");
        return match.Success?SourceGroupPolicy.CanonicalUrl("https://www.facebook.com/groups/"+match.Groups[1].Value+"/"):null;
    }
}
