using System.Text.Json.Nodes;
using Microsoft.Playwright;
using SammiCrawl.Backend.Services;

using var pw = await Playwright.CreateAsync();
await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
var page = await browser.NewPageAsync();
await page.RouteAsync("**/*", route => route.AbortAsync());
var checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception("FAIL: " + label); checks++; Console.WriteLine("PASS: " + label); }
const string source = "https://www.facebook.com/groups/123/posts/456/";
async Task<JsonObject> Read(string html)
{
    await page.SetContentAsync("<html lang='vi'><body><section role='article' id='post'>" + html + "</section></body></html>");
    var json = await page.Locator("#post").EvaluateAsync<string>("root => JSON.stringify((" + FacebookPostMetadata.Script + ")(root, '" + source + "'))");
    return JsonNode.Parse(json)!.AsObject();
}
var data = await Read("<a href='" + source + "'><abbr data-utime='1793232000'>29 tháng 10</abbr></a><span>123 cảm xúc</span><span>45 bình luận</span>");
Check(data["reactions"]?["value"]?.GetValue<int>() == 123 && data["comments"]?["value"]?.GetValue<int>() == 45, "Vietnamese exact reaction and comment counters extracted");
Check(data["likes"] is null, "total reactions not falsely labelled as Likes");
Check(data["posted"]?["unix"]?.ToString() == "1793232000", "source data-utime extracted");
data = await Read("<button aria-label='All reactions: 1.2K'>1.2K</button><a>View all 1,234 comments</a>");
Check(data["reactions"]?["value"]?.GetValue<int>() == 1200 && data["reactions"]?["approximate"]?.GetValue<bool>() == true, "English abbreviated reactions kept approximate");
Check(data["comments"]?["value"]?.GetValue<int>() == 1234 && data["comments"]?["approximate"]?.GetValue<bool>() == false, "English thousands grouping is exact");
data = await Read("<button aria-label='Xem ai đã bày tỏ cảm xúc về tin này'>1,2K</button><span>2,5 nghìn bình luận</span>");
Check(data["reactions"]?["value"]?.GetValue<int>() == 1200 && data["reactions"]?["text"]?.ToString() == "1,2K", "labelled summary button and Vietnamese decimal comma supported");
Check(data["comments"]?["value"]?.GetValue<int>() == 2500, "Vietnamese word unit supported");
data = await Read("<span>1.234 bình luận</span><span>Thích: 12 người</span>");
Check(data["comments"]?["value"]?.GetValue<int>() == 1234 && data["likes"]?["value"]?.GetValue<int>() == 12 && data["reactions"] is null, "Vietnamese grouping and explicit Likes stay distinct from total reactions");
data = await Read("<span>0 cảm xúc</span><span>0 bình luận</span>");
Check(data["reactions"]?["value"]?.GetValue<int>() == 0 && data["comments"]?["value"]?.GetValue<int>() == 0, "explicit zero is retained");
data = await Read("<button aria-label='Thích'>Thích</button><button>Bình luận</button>");
Check(data["reactions"] is null && data["likes"] is null && data["comments"] is null, "missing counters are unknown, never fabricated zero");
data = await Read("<div data-ad-preview='message'><span>999 bình luận</span><span>888 cảm xúc</span></div><article role='article'><span>777 cảm xúc</span><span>666 bình luận</span></article><span hidden>555 bình luận</span><span style='display:none'>444 cảm xúc</span><span>5 cảm xúc</span><span>3 bình luận</span>");
Check(data["reactions"]?["value"]?.GetValue<int>() == 5 && data["comments"]?["value"]?.GetValue<int>() == 3, "caption numbers, nested replies and hidden counters cannot contaminate totals");
data = await Read("<span>Xem thêm 5 bình luận</span><span>View more 7 comments</span><span>1,2 bình luận</span><span>99999999999999999 cảm xúc</span>");
Check(data["reactions"] is null && data["comments"] is null, "remaining-comment controls, ambiguous decimal and unsafe integers rejected");
data = await Read("<a href='" + source + "'>2 giờ</a><a href='" + source + "?comment_id=1'><time datetime='2026-10-29T09:00:00+07:00'>Mới</time></a>");
Check(data["posted"]?["text"]?.ToString() == "2 giờ" && data["posted"]?["datetime"] is null, "relative source label preserved without borrowing a comment timestamp");
data = await Read("<a href='" + source + "'><time datetime='2026-10-29T09:28:26+07:00'>29 tháng 10</time></a><a href='" + source + "'>12 bình luận</a>");
Check(data["posted"]?["datetime"]?.ToString() == "2026-10-29T09:28:26+07:00", "machine-readable original publication time retained");
var captured = DateTimeOffset.Parse("2026-10-29T12:00:00Z");
var article = new JsonObject { ["createdAt"] = "2026-10-29T10:00:00Z" };
FacebookPostMetadata.Apply(article, data, captured);
Check(article["sourcePublishedAt"]?.ToString() == "2026-10-29T02:28:26.0000000+00:00" && article["createdAt"]?.ToString() == "2026-10-29T10:00:00Z", "published UTC timestamp separate from immutable collection timestamp");
Check(article["interactionCapturedAt"]?.ToString() == captured.ToString("O"), "interaction observation timestamp recorded");
data = await Read("<a href='" + source + "'>12 bình luận</a>");
Check(data["posted"] is null, "comment link count is not a publication time");
data = await Read("<a href='" + source + "'>2 giờ</a><a href='https://www.facebook.com/groups/123/posts/789/'>1 giờ</a><span>500 cảm xúc</span>");
Check(data["posted"] is null && data["reactions"] is null, "ambiguous multi-post container not attributed to one source");
article = new JsonObject();
FacebookPostMetadata.Apply(article, JsonNode.Parse("{\"comments\":{\"value\":-1},\"posted\":{\"datetime\":\"2026-10-29T09:28:26\",\"text\":\"2 giờ\"}}"), captured);
Check(article["sourcePublishedAt"] is null && article["comments"] is null && article["sourcePublishedText"]?.ToString() == "2 giờ", "no invented timezone or invalid negative count");
article = new JsonObject();
FacebookPostMetadata.Apply(article, JsonNode.Parse("{\"posted\":{\"unix\":\"999999999999999999\",\"datetime\":\"2099-01-01T00:00:00Z\"}}"), captured);
Check(article["sourcePublishedAt"] is null, "out-of-range and future timestamps cannot become source publication time");
Console.WriteLine($"Facebook metadata checks passed: {checks}. Fixture HTML only; no database, Facebook, or AI.");
