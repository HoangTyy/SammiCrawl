using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SammiCrawl.Backend.Services;

public static class SourceGroupPolicy
{
    public static string? CanonicalUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var url) || url.Scheme != "https" || url.Port != 443
            || url.UserInfo.Length > 0 || !(url.Host == "facebook.com" || url.Host.EndsWith(".facebook.com"))) return null;
        var match = Regex.Match(url.AbsolutePath, @"^/groups/([A-Za-z0-9._-]+)/?$", RegexOptions.CultureInvariant);
        if (!match.Success || new[] { "feed", "discover", "search", "joins", "create" }.Contains(match.Groups[1].Value.ToLowerInvariant())) return null;
        return "https://www.facebook.com/groups/" + match.Groups[1].Value.ToLowerInvariant() + "/";
    }

    public static string[] Keywords(string? value) => (value ?? "").Split(['\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToArray();
    public static Guid Id(Guid brandId, string url) => new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(brandId + ":" + url))[..16]);
    public static bool Accepted(JsonNode article) => article["deletedAt"] is null && article["aiStatus"]?.ToString() == "recommended";
    public static void ApplyChannelVerdict(JsonObject article, JsonArray matches)
    {
        if (article["aiStatus"]?.ToString() == "rejected") return;
        if (matches.Count == 0) throw new InvalidOperationException("Chưa có đánh giá kênh.");
        foreach (var match in matches)
            if (match?["status"]?.ToString() is not ("recommended" or "review" or "rejected")
                || !double.TryParse(match["score"]?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var score)
                || !double.IsFinite(score) || score < 0 || score > 100) throw AiGatewayException.InvalidOutput();
        if (matches.All(c => c?["status"]?.ToString() == "rejected"))
        {
            article["aiStatus"] = "rejected";
            article["aiReason"] = "Clef xác định bài không phù hợp với các kênh đang bật của thương hiệu.";
        }
        else if (!matches.Any(c => c?["status"]?.ToString() == "recommended")) article["aiStatus"] = "review";
    }
}
