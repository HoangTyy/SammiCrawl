using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public static class CrawlAccountSelection
{
    public static string? Validate(JsonNode? value, IReadOnlySet<Guid> eligible, out Guid[] selected)
    {
        selected = [];
        if (value is not JsonArray ids || ids.Count == 0)
            return "Chọn ít nhất một tài khoản Facebook để cào bài.";
        var result = new List<Guid>();
        foreach (var node in ids)
        {
            if (node is not JsonValue v || !v.TryGetValue<string>(out var text) || !Guid.TryParse(text, out var id))
                return "Danh sách tài khoản cào không hợp lệ.";
            if (!eligible.Contains(id))
                return "Có tài khoản đã chọn không còn đủ điều kiện cào. Chọn lại tài khoản đang bật và hoạt động.";
            if (!result.Contains(id)) result.Add(id);
        }
        selected = result.ToArray();
        return null;
    }
}
