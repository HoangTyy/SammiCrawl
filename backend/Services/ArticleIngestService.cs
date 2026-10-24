using System.Text.Json.Nodes;
namespace SammiCrawl.Backend.Services;
public sealed class ArticleIngestService(IProjectRepository db)
{
    public async Task<ServiceResult> Ingest(JsonObject input)
    {
        if (!Guid.TryParse(input["brandId"]?.ToString(), out var brandId)) return ServiceResult.BadRequest(new { error = "Cần thương hiệu hợp lệ." });
        var brand = await db.FindDocumentAsync(brandId, "brands");
        if (brand is null || JsonNode.Parse(brand.Json)?["deletedAt"] is not null) return ServiceResult.BadRequest(new { error = "Thương hiệu không còn hoạt động." });
        var source = input["sourceUrl"]?.ToString() ?? "";
        if (ArticleDeduplication.SourceKey(source) is null) return ServiceResult.BadRequest(new { error = "Cần link bài Facebook cụ thể (posts/permalink/story_fbid/reel/videos), không dùng link nhóm hoặc bảng tin." });
        if (input["media"] is JsonArray media && media.Count > 0) return ServiceResult.BadRequest(new { error = "Điểm nhận bài hiện chưa xử lý tải media; không thể nhận và bỏ qua media một cách im lặng." });
        var text = input["content"]?.ToString() ?? "";
        if (text.Length > 200000) return ServiceResult.BadRequest(new { error = "Nội dung bài quá dài." });
        var article = new JsonObject { ["sourceUrl"] = source, ["content"] = text, ["title"] = input["title"]?.ToString(), ["aiStatus"] = "pending", ["media"] = new JsonArray() };
        var result = await db.IngestArticleAsync(brandId, article);
        return ServiceResult.Ok(new { article = JsonNode.Parse(result.Record.Json), duplicate = result.Duplicate, duplicateReason = result.Reason });
    }
}
