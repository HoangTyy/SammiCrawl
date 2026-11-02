using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace SammiCrawl.Backend.Services;

public static class SourceDiscovery
{
    public static JsonObject Profile(JsonNode brand)
    {
        var result=new JsonObject();
        foreach(var field in new[]{"name","description","audience","allowedTopics","forbiddenContent"})
            result[field]=brand[field]?.ToString() is string value ? value[..Math.Min(value.Length,1500)] : "";
        return result;
    }
    public static string[] Parse(string output)
    {
        var lines=output.Trim().Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(lines.Length is <1 or >6)throw new InvalidOperationException("AI cần trả 1–6 từ khóa tìm nhóm.");
        var words=lines.Select(line=>Regex.Replace(line,@"^[-*•\d.\s]+", "").Trim()).ToArray();
        if(words.Any(w=>w.Length is <2 or >80 || w.Contains(":") || w.Contains("/") || w.Contains("<") || w.Contains("{") || w.Contains("```")))
            throw new InvalidOperationException("AI trả từ khóa tìm nhóm không hợp lệ; chưa truy cập Facebook.");
        return words.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
