using Microsoft.Playwright;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public sealed record FacebookLoginRequest(string Email, string Password);
public sealed class FacebookLoginService(IProjectRepository db, IDataProtectionProvider protection, FacebookAccountHealth health)
{
    public async Task<ServiceResult> Login(Guid id, FacebookLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password)) return ServiceResult.BadRequest(new { error = "Nhập email và mật khẩu Facebook." });
        var record = await db.FindDocumentAsync(id, "accounts");
        if (record is null) return ServiceResult.NotFound();
        var data = JsonNode.Parse(record.Json)!.AsObject();
        if (data["deletedAt"] is not null) return ServiceResult.Conflict(new { error = "Khôi phục tài khoản trước khi kết nối." });
        FacebookCheck result; string stage = "khởi động trình duyệt";
        try
        {
            using var pw = await Playwright.CreateAsync();
            await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
            await using var context = await browser.NewContextAsync(new() { Locale = "en-US" });
            var page = await context.NewPageAsync(); page.SetDefaultTimeout(15000);
            stage = "mở Facebook";
            await page.GotoAsync("https://www.facebook.com/login/", new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            stage = "tìm ô email";
            await page.GetByRole(AriaRole.Textbox, new() { NameRegex = new System.Text.RegularExpressions.Regex("Email|mobile|điện thoại", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).First.FillAsync(request.Email);
            stage = "tìm ô mật khẩu";
            await page.Locator("input[type=password]").FillAsync(request.Password);
            stage = "gửi đăng nhập";
            await page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
            // Give Facebook time to finish its own automatic security checks; never solve challenges.
            await page.WaitForTimeoutAsync(6000);
            var cookies = await context.CookiesAsync(new[] { "https://www.facebook.com/" });
            var uid = cookies.FirstOrDefault(c => c.Name == "c_user")?.Value;
            if (!string.IsNullOrEmpty(uid) && cookies.Any(c => c.Name == "xs"))
            {
                var expected = data["uid"]?.ToString();
                if (!string.IsNullOrEmpty(expected) && expected != uid) result = new("unknown", "IDENTITY_MISMATCH", "UID đăng nhập không khớp tài khoản đã chọn.");
                else
                {
                    var raw = System.Text.Json.JsonSerializer.Serialize(cookies.Select(c => new { name=c.Name, value=c.Value, domain=c.Domain }));
                    record.ProtectedSecret = protection.CreateProtector("FacebookSession.v1").Protect(raw);
                    data["uid"] = uid; record.Json = data.ToJsonString(); await db.SaveChangesAsync();
                    return ServiceResult.Ok(await health.CheckAndSave(id, db));
                }
            }
            else
            {
                var text = (await page.Locator("body").InnerTextAsync()).ToLowerInvariant();
                result = page.Url.Contains("checkpoint") || page.Url.Contains("two_step_verification") || text.Contains("security check") || text.Contains("captcha")
                    ? new("checkpoint", "CHECKPOINT", "Facebook yêu cầu xác minh trên phiên kết nối của SammiCrawl. Chưa lưu được phiên; không tự vượt xác minh.")
                    : new("unknown", "LOGIN_NOT_CONFIRMED", "Facebook chưa cấp phiên đăng nhập. Kiểm tra thông tin hoặc đăng nhập tương tác và nhập cookie.");
            }
        }
        catch { result = new("unknown", "LOGIN_UNAVAILABLE", $"Không hoàn tất bước {stage} từ SammiCrawl. Chưa kết luận tài khoản bị khóa."); }
        FacebookAccountHealth.Apply(data, result, "live_login"); record.Json = data.ToJsonString();
        db.AddLog(new Audit { Message = $"Kết nối Facebook {id}: {result.Code}" }); await db.SaveChangesAsync();
        return ServiceResult.Ok(data);
    }
}
