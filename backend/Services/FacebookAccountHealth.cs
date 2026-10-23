using Microsoft.Playwright;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SammiCrawl.Backend.Services;

public record FacebookCheck(string Status, string Code, string Reason, string? Uid = null);
public sealed class FacebookAccountHealth(IDataProtectionProvider protection)
{
    readonly SemaphoreSlim browserGate = new(1, 1);
    public static readonly string[] Allowed = ["unknown", "active", "locked", "checkpoint", "expired", "restricted", "invalid_cookie"];
    public async Task<JsonObject?> CheckAndSave(Guid id, IProjectRepository db)
    {
        var record = await db.FindDocumentAsync(id, "accounts"); if (record is null) return null;
        var originalSecret = record.ProtectedSecret; string? cookie = null;
        try { if (originalSecret is not null) cookie = protection.CreateProtector("FacebookSession.v1").Unprotect(originalSecret); }
        catch { /* Never log protected material. */ }
        var result = await Check(cookie);
        await db.ReloadAsync(record);
        if (record.ProtectedSecret != originalSecret) return JsonNode.Parse(record.Json)!.AsObject();
        var data = JsonNode.Parse(record.Json)!.AsObject();
        var expectedUid = data["uid"]?.ToString();
        if (result.Status == "active" && !string.IsNullOrEmpty(expectedUid) && expectedUid != result.Uid) result = new("unknown", "IDENTITY_MISMATCH", "Phiên đăng nhập không khớp UID đã khai báo. Kiểm tra lại cookie và UID.");
        Apply(data, result, "live_check");
        if (result.Status == "active" && result.Uid is not null) data["uid"] = result.Uid;
        record.Json = data.ToJsonString(); db.AddLog(new Audit { Message = $"Kiểm tra Facebook {id}: {result.Code} · {result.Reason}" }); await db.SaveChangesAsync(); return data;
    }
    public static void Apply(JsonObject data, FacebookCheck result, string source)
    {
        data["facebookStatus"] = result.Status; data["facebookStatusSource"] = source; data["facebookCheckedAt"] = DateTimeOffset.UtcNow.ToString("O");
        data["facebookErrorCode"] = result.Code; data["facebookStatusReason"] = result.Reason;
        data["sessionStatus"] = result.Status switch { "active" => "Đã xác minh phiên Facebook", "expired" => "Phiên hết hạn / cần đăng nhập lại", "locked" => "Facebook khóa hoặc đình chỉ tài khoản", "checkpoint" => "Facebook yêu cầu xác minh", "restricted" => "Facebook giới hạn tạm thời", "invalid_cookie" => "Cookie không hợp lệ", _ => "Chưa xác minh được phiên" };
    }
    public static FacebookCheck Runtime(string code) => code switch
    {
        "LOGIN_REQUIRED" => new("expired", code, "Facebook yêu cầu đăng nhập lại khi thực hiện tác vụ."),
        "ACCOUNT_LOCKED" => new("locked", code, "Facebook thông báo tài khoản bị khóa/đình chỉ khi thực hiện tác vụ."),
        "CHECKPOINT" => new("checkpoint", code, "Facebook yêu cầu xác minh. Tác vụ cần dừng tài khoản này."),
        "RATE_LIMITED" => new("restricted", code, "Facebook giới hạn tạm thời. Dừng tài khoản và chờ trước khi thử lại."),
        "PERMISSION_DENIED" => new("unknown", code, "Không đủ quyền tại Page/nhóm đích; chưa kết luận tài khoản bị khóa."),
        "NETWORK_ERROR" => new("unknown", code, "Lỗi kết nối khi thực hiện tác vụ; chưa kết luận lỗi tài khoản."),
        _ => new("unknown", "UNKNOWN_ERROR", "Tác vụ gặp lỗi chưa phân loại; cần xem log tác vụ.")
    };
    public async Task<FacebookCheck> Check(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new("unknown", "MISSING_COOKIE", "Chưa có cookie để kiểm tra Facebook.");
        if (raw.Length > 100000) return new("invalid_cookie", "INVALID_COOKIE", "Cookie quá dài hoặc sai định dạng.");
        List<Cookie> cookies = []; string? uid;
        try
        {
            if (raw.TrimStart().StartsWith('[') || raw.TrimStart().StartsWith('{'))
            {
                var parsed = JsonNode.Parse(raw); var array = parsed as JsonArray ?? parsed?["cookies"] as JsonArray ?? throw new FormatException();
                foreach (var item in array)
                {
                    var domain = item?["domain"]?.ToString()?.TrimStart('.') ?? "facebook.com";
                    if (domain != "facebook.com" && !domain.EndsWith(".facebook.com")) continue;
                    var name = item?["name"]?.ToString(); var value = item?["value"]?.ToString();
                    if (name is not null && value is not null && Regex.IsMatch(name, "^[a-zA-Z0-9_]+$")) cookies.Add(new Cookie { Name = name, Value = value, Domain = ".facebook.com", Path = "/", Secure = true });
                }
            }
            else foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Trim().Split('=', 2); if (pair.Length == 2 && Regex.IsMatch(pair[0], "^[a-zA-Z0-9_]+$")) cookies.Add(new Cookie { Name = pair[0], Value = pair[1], Domain = ".facebook.com", Path = "/", Secure = true });
            }
            cookies = cookies.GroupBy(c => c.Name).Select(g => g.Last()).ToList(); uid = cookies.Find(c => c.Name == "c_user")?.Value;
            if (uid is null || !Regex.IsMatch(uid, "^[0-9]{1,30}$") || string.IsNullOrEmpty(cookies.Find(c => c.Name == "xs")?.Value)) return new("invalid_cookie", "INVALID_COOKIE", "Cookie thiếu c_user hoặc xs hợp lệ. Cần lấy lại phiên đăng nhập Facebook.");
        }
        catch { return new("invalid_cookie", "INVALID_COOKIE", "Cookie không đúng định dạng chuỗi hoặc JSON export."); }
        if (!await browserGate.WaitAsync(TimeSpan.FromSeconds(5))) return new("unknown", "CHECK_BUSY", "Đang kiểm tra tài khoản khác. Bấm kiểm tra lại sau.");
        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Timeout = 20000 });
            await using var context = await browser.NewContextAsync(new() { Locale = "en-US", ViewportSize = new() { Width = 1280, Height = 900 } });
            await context.AddCookiesAsync(cookies);
            await context.RouteAsync("**/*", async route =>
            {
                if (route.Request.ResourceType is "image" or "media" or "font") await route.AbortAsync(); else await route.ContinueAsync();
            });
            var page = await context.NewPageAsync(); page.SetDefaultTimeout(10000);
            var response = await page.GotoAsync("https://www.facebook.com/me", new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await page.WaitForTimeoutAsync(1500);
            var url = new Uri(page.Url); if (url.Host != "facebook.com" && !url.Host.EndsWith(".facebook.com")) return new("unknown", "UNEXPECTED_REDIRECT", "Facebook chuyển hướng ngoài trang kiểm tra; chưa xác minh được.");
            var path = url.AbsolutePath.ToLowerInvariant();
            var text = (await page.Locator("body").InnerTextAsync()).ToLowerInvariant();
            if (text.Contains("we suspended your account") || text.Contains("your account has been disabled") || text.Contains("your account has been suspended") || text.Contains("your account has been locked") || text.Contains("we've locked your account") || text.Contains("tài khoản của bạn đã bị khóa") || text.Contains("chúng tôi đã đình chỉ tài khoản")) return new("locked", "ACCOUNT_LOCKED", "Facebook hiển thị thông báo khóa hoặc đình chỉ tài khoản.");
            if (path.Contains("checkpoint") || path.Contains("two_step_verification") || text.Contains("confirm your identity") || text.Contains("xác nhận danh tính")) return new("checkpoint", "CHECKPOINT", "Facebook yêu cầu xác minh danh tính hoặc bảo mật. Cần xử lý trực tiếp trên Facebook.");
            if (path.Contains("/login") || path.Contains("/recover") || await page.Locator("input[name='email']").CountAsync() > 0) return new("expired", "LOGIN_REQUIRED", "Facebook trả về trang đăng nhập. Phiên không còn được chấp nhận.");
            if (response?.Status is 429 || text.Contains("you're temporarily blocked") || text.Contains("temporarily blocked from")) return new("restricted", "RATE_LIMITED", "Facebook giới hạn truy cập tạm thời. Không tự thử lặp lại.");
            var authMenu = page.Locator("[aria-label='Your profile'],[aria-label='Account'],[aria-label='Trang cá nhân của bạn'],[aria-label='Tài khoản'],a[href*='/logout.php']");
            if (response is not null && response.Status < 400 && await authMenu.CountAsync() > 0 && await authMenu.First.IsVisibleAsync() && (await context.CookiesAsync()).Any(c => c.Name == "c_user" && c.Value == uid)) return new("active", "SESSION_OK", "Đã mở trang cá nhân và nhận diện giao diện tài khoản đã đăng nhập.", uid);
            return new("unknown", "UNVERIFIED_RESPONSE", "Đã truy cập Facebook nhưng chưa đủ dấu hiệu xác nhận phiên. Không tự kết luận tài khoản bị khóa.");
        }
        catch (System.TimeoutException) { return new("unknown", "CHECK_TIMEOUT", "Facebook tải quá thời gian kiểm tra. Chưa kết luận lỗi tài khoản."); }
        catch { return new("unknown", "CHECK_UNAVAILABLE", "Không hoàn tất kết nối/trình duyệt kiểm tra Facebook. Chưa kết luận lỗi tài khoản."); }
        finally { browserGate.Release(); }
    }
}
