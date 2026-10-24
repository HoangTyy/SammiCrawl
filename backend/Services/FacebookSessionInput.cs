using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace SammiCrawl.Backend.Services;
public static class FacebookSessionInput
{
    public static string? Normalize(JsonObject data)
    {
        if (!data.ContainsKey("xs")) return null;
        var xs = data["xs"]?.ToString().Trim().Trim('"') ?? "";
        data.Remove("xs");
        if (string.IsNullOrWhiteSpace(xs)) { data.Remove("cookie"); return null; }
        var uid = data["uid"]?.ToString().Trim() ?? "";
        if (!Regex.IsMatch(uid, "^[0-9]{1,30}$")) return "Cần UID Facebook chỉ gồm chữ số khi nhập giá trị xs.";
        if (xs.Length > 10000 || xs.Any(c => char.IsWhiteSpace(c) || c == ';' || c == '=')) return "Chỉ nhập giá trị xs, không nhập xs= hoặc toàn bộ cookie.";
        data["uid"] = uid; data["cookie"] = $"c_user={uid}; xs={xs};";
        return null;
    }
    public static string ExtractXs(string raw)
    {
        if (raw.TrimStart().StartsWith('[') || raw.TrimStart().StartsWith('{'))
        {
            var node = JsonNode.Parse(raw); var entries = node as JsonArray ?? node?["cookies"] as JsonArray;
            return entries?.LastOrDefault(e => e?["name"]?.ToString() == "xs")?["value"]?.ToString() ?? "";
        }
        return raw.Split(';').Select(p => p.Trim().Split('=', 2)).LastOrDefault(p => p.Length == 2 && p[0] == "xs")?.ElementAt(1) ?? "";
    }
}
