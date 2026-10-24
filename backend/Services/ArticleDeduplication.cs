using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
namespace SammiCrawl.Backend.Services;
public static class ArticleDeduplication
{
    public static string NormalizeText(string text) => Regex.Replace(text.Normalize(NormalizationForm.FormKC).Replace("\u200B", "").Replace("\uFEFF", ""), @"\s+", " ").Trim().ToUpperInvariant();
    public static string? ContentKey(JsonObject article)
    {
        var text = NormalizeText(article["content"]?.ToString() ?? "");
        return text.Length == 0 ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
    public static string? SourceKey(string raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var u) || u.Scheme != "https" || !(u.Host == "facebook.com" || u.Host.EndsWith(".facebook.com"))) return null;
        var match = Regex.Match(u.AbsolutePath, @"/(?:posts|permalink)/(\d+)(?:/|$)");
        if (match.Success) return "post:" + match.Groups[1].Value;
        var query = u.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).Where(p => p.Length == 2).GroupBy(p => p[0], StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Last()[1], StringComparer.OrdinalIgnoreCase);
        if (query.TryGetValue("story_fbid", out var id) && Regex.IsMatch(id, @"^\d+$")) return "post:" + id;
        match = Regex.Match(u.AbsolutePath, @"/(?:reel|videos)/(\d+)(?:/|$)");
        if (match.Success) return "video:" + match.Groups[1].Value;
        // Feed/group URLs identify a collection, never a single post.
        return null;
    }
    public static string? Match(JsonObject candidate, JsonObject existing)
    {
        var source = SourceKey(candidate["sourceUrl"]?.ToString() ?? "");
        if (source is not null && source == SourceKey(existing["sourceUrl"]?.ToString() ?? "")) return "source";
        var content = ContentKey(candidate);
        return content is not null && content == ContentKey(existing) ? "content" : null;
    }
}
