using System.Text.Json.Nodes;
namespace SammiCrawl.Backend.Services;

public interface IChannelsService
{
    Task<ServiceResult> List();
    Task<ServiceResult> Create(Guid brandId, JsonObject request);
    Task<ServiceResult> Update(Guid brandId, string channelId, JsonObject request);
}
public sealed class ChannelsService(IProjectRepository db) : IChannelsService
{
    static readonly string[] Fields = ["name", "type", "url", "dailyTarget", "minWords", "maxWords", "audience", "tone", "contentGuidelines", "imageRequirements", "requireBranding", "active", "publishAccountId"];
    public async Task<ServiceResult> List()
    {
        var brands = await db.ListDocumentsAsync("brands");
        var result = new JsonArray();
        foreach (var record in brands)
        {
            var profile = JsonNode.Parse(record.Json)!;
            if (profile["deletedAt"] is not null) continue;
            foreach (var channel in BrandPolicy.Channels(profile))
            {
                if (channel is not JsonObject item) continue;
                item["brandId"] = record.Id.ToString(); item["brandName"] = profile["name"]?.ToString();
                item["postingPermission"] = "unverified"; item["brandVersion"] = profile["updatedAt"]?.ToString() ?? profile["createdAt"]?.ToString();
                result.Add(item.DeepClone());
            }
        }
        return ServiceResult.Ok(result);
    }
    public Task<ServiceResult> Create(Guid brandId, JsonObject request) => Save(brandId, null, request);
    public Task<ServiceResult> Update(Guid brandId, string channelId, JsonObject request) => Save(brandId, channelId, request);
    async Task<ServiceResult> Save(Guid brandId, string? channelId, JsonObject request)
    {
        var record = await db.FindDocumentAsync(brandId, "brands");
        var profile = record is null ? null : JsonNode.Parse(record.Json)!.AsObject();
        if (profile is null || profile["deletedAt"] is not null) return ServiceResult.NotFound(new { error = "Thương hiệu không còn hoạt động." });
        var version = profile["updatedAt"]?.ToString() ?? profile["createdAt"]?.ToString();
        if (request.ContainsKey("brandVersion") && request["brandVersion"]?.ToString() != version) return ServiceResult.Conflict(new { error = "Hồ sơ kênh vừa đổi ở nơi khác. Làm mới trước khi lưu." });
        var channels = BrandPolicy.Channels(profile);
        var item = channelId is null ? new JsonObject { ["id"] = Guid.NewGuid().ToString(), ["type"] = "group", ["active"] = true, ["dailyTarget"] = 0, ["minWords"] = 80, ["maxWords"] = 250, ["requireBranding"] = false } : channels.FirstOrDefault(c => c?["id"]?.ToString() == channelId) as JsonObject;
        if (item is null) return ServiceResult.NotFound(new { error = "Không tìm thấy kênh trong thương hiệu này." });
        if (channelId is null && request["type"]?.ToString() == "page") { item["minWords"] = 50; item["maxWords"] = 150; item["requireBranding"] = true; }
        foreach (var field in Fields) if (request.ContainsKey(field)) item[field] = request[field]?.DeepClone();
        if (channelId is null) channels.Add(item);
        profile["channels"] = channels;
        if (BrandPolicy.Validate(profile) is string policyError) return ServiceResult.BadRequest(new { error = policyError });
        if (await BrandPolicy.ValidatePublishers(profile, db) is string publisherError) return ServiceResult.BadRequest(new { error = publisherError });
        profile["updatedAt"] = DateTimeOffset.UtcNow.ToString("O");
        if (!await db.TryUpdateBrandAsync(brandId, record!.Json, profile.ToJsonString(), $"Đã {(channelId is null ? "thêm" : "cập nhật")} kênh {item["id"]} · thương hiệu {brandId}"))
            return ServiceResult.Conflict(new { error = "Hồ sơ vừa đổi ở nơi khác. Làm mới danh sách trước khi lưu." });
        var response = item.DeepClone().AsObject(); response["brandId"] = brandId.ToString(); response["brandName"] = profile["name"]?.ToString(); response["postingPermission"] = "unverified"; response["brandVersion"] = profile["updatedAt"]?.ToString();
        return ServiceResult.Ok(response);
    }
}
