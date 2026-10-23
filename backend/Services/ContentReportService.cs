using System.Globalization;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public interface IContentReportService { Task<ServiceResult> Overview(string? date); }
public sealed class ContentReportService(IProjectRepository db) : IContentReportService
{
    public async Task<ServiceResult> Overview(string? date)
    {
        var day = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        if (date is not null && !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) return ServiceResult.BadRequest(new { error = "Ngày báo cáo cần định dạng yyyy-MM-dd." });
        var start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)); var end = start.AddDays(1);
        var brands = (await db.ListDocumentsAsync("brands")).Select(d => JsonNode.Parse(d.Json)!).Where(b => b["deletedAt"] is null).ToList();
        var activeBrandIds = brands.Select(b => b["id"]!.ToString()).ToHashSet();
        var articles = (await db.ListDocumentsAsync("articles")).Select(d => JsonNode.Parse(d.Json)!).Where(a => activeBrandIds.Contains(a["brandId"]?.ToString() ?? "")).ToList();
        var drafts = (await db.ListDocumentsAsync("drafts")).Select(d => JsonNode.Parse(d.Json)!).Where(d => activeBrandIds.Contains(d["brandId"]?.ToString() ?? "")).ToList();
        bool Within(JsonNode node, string field) => DateTimeOffset.TryParse(node[field]?.ToString(), out var instant) && instant >= start && instant < end;
        bool Published(JsonNode node) => node["status"]?.ToString() == "published" && BrandPolicy.FacebookPostUrl(node["publishedUrl"]?.ToString()) && Within(node, "publishedAt") && DateTimeOffset.TryParse(node["publishedAt"]?.ToString(), out var publishedAt) && publishedAt <= DateTimeOffset.UtcNow;
        var channels = brands.SelectMany(brand => BrandPolicy.Channels(brand).Where(channel => channel is not null && BrandPolicy.Flag(channel, "active")).Select(channel => {
            var own = drafts.Where(d => d["brandId"]?.ToString() == brand["id"]?.ToString() && (d["channelId"]?.ToString() ?? "legacy-group") == channel!["id"]?.ToString()).ToList();
            var published = own.Count(Published); var target = BrandPolicy.Number(channel!, "dailyTarget", 0);
            return new { brandId = brand["id"]!.ToString(), brandName = brand["name"]?.ToString(), channelId = channel!["id"]?.ToString(), channelName = channel["name"]?.ToString(), type = channel["type"]?.ToString(), url = channel["url"]?.ToString(), dailyTarget = target,
                awaitingReview = own.Count(d => d["status"]?.ToString() == "awaiting_review"), approvedPendingWorker = own.Count(d => d["status"]?.ToString() == "approved_pending_worker"),
                planned = own.Count(d => d["status"]?.ToString() is "awaiting_review" or "approved_pending_worker" && Within(d, "plannedAt")), published, missing = Math.Max(0, target - published) };
        })).ToArray();
        var calendar = drafts.Where(d => d["status"]?.ToString() is "awaiting_review" or "approved_pending_worker" && Within(d, "plannedAt")).OrderBy(d => d["plannedAt"]?.ToString()).Select(d => new {
            id = d["id"]?.ToString(), brandId = d["brandId"]?.ToString(), channelName = d["channelSnapshot"]?["name"]?.ToString() ?? "Kênh cũ", content = d["content"]?.ToString(), plannedAt = d["plannedAt"]?.ToString(), status = d["status"]?.ToString()
        }).ToArray();
        return ServiceResult.Ok(new { date = day.ToString("yyyy-MM-dd"), timezone = "Asia/Ho_Chi_Minh", collected = articles.Count, recommended = articles.Count(a => a["aiStatus"]?.ToString() == "recommended" && a["aiBrandUpdatedAt"]?.ToString() == brands.First(b => b["id"]?.ToString() == a["brandId"]?.ToString())["updatedAt"]?.ToString()),
            awaitingReview = drafts.Count(d => d["status"]?.ToString() == "awaiting_review"), approvedPendingWorker = drafts.Count(d => d["status"]?.ToString() == "approved_pending_worker"),
            published = channels.Sum(c => c.published), dailyTarget = channels.Sum(c => c.dailyTarget), channels, calendar, workerAvailable = false });
    }
}
