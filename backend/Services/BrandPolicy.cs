using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SammiCrawl.Backend.Services;

public static class BrandPolicy
{
    public static readonly string[] Fields = ["allowedTopics", "forbiddenContent", "verifyInformation", "requireMediaRights", "requireSponsorLabel", "channels"];
    public static bool FacebookUrl(string? text, string? type = null) => Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && (uri.Host == "facebook.com" || uri.Host.EndsWith(".facebook.com", StringComparison.OrdinalIgnoreCase))
        && uri.AbsolutePath != "/" && (type != "group" || uri.AbsolutePath.StartsWith("/groups/"))
        && (type != "page" || !uri.AbsolutePath.StartsWith("/groups/"));
    public static bool FacebookPostUrl(string? text) => FacebookUrl(text) && Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && (uri.AbsolutePath.Contains("/posts/") || uri.AbsolutePath.Contains("/permalink/") || uri.AbsolutePath.Contains("/videos/") || uri.AbsolutePath.StartsWith("/reel/") || uri.AbsolutePath.StartsWith("/photo") || uri.Query.Contains("story_fbid="));
    public static bool Flag(JsonNode? policy, string key, bool fallback = true) => policy?[key] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : fallback;
    public static int Number(JsonNode policy, string key, int fallback) => policy[key] is JsonValue value && value.TryGetValue<int>(out var number) ? number : fallback;
    public static JsonArray Channels(JsonNode profile)
    {
        if (profile["channels"] is JsonArray channels) return (JsonArray)channels.DeepClone();
        var url = profile["facebookGroupUrl"]?.ToString();
        return FacebookUrl(url, "group") ? new JsonArray(new JsonObject { ["id"] = "legacy-group", ["name"] = "Nhóm Facebook", ["type"] = "group", ["url"] = url, ["dailyTarget"] = 0, ["minWords"] = 80, ["maxWords"] = 250, ["active"] = true, ["requireBranding"] = false }) : new JsonArray();
    }
    public static string? Validate(JsonObject profile)
    {
        foreach (var field in new[] { "allowedTopics", "forbiddenContent" })
            if (profile[field] is not null && (profile[field] is not JsonValue value || !value.TryGetValue<string>(out var text) || text.Length > 10000)) return $"{field} phải là văn bản tối đa 10.000 ký tự.";
        foreach (var field in new[] { "verifyInformation", "requireMediaRights", "requireSponsorLabel" })
            if (profile[field] is not null && (profile[field] is not JsonValue value || !value.TryGetValue<bool>(out _))) return $"{field} phải là lựa chọn bật/tắt.";
        if (!profile.ContainsKey("channels")) return null;
        if (profile["channels"] is not JsonArray channels || channels.Count > 30) return "Mỗi thương hiệu có tối đa 30 kênh đăng.";
        var ids = new HashSet<string>(); var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in channels)
        {
            if (item is not JsonObject channel) return "Kênh đăng không hợp lệ.";
            var id = channel["id"]?.ToString(); var type = channel["type"]?.ToString(); var url = channel["url"]?.ToString();
            if (string.IsNullOrWhiteSpace(id) || id.Length > 80 || !ids.Add(id)) return "ID kênh trống hoặc bị trùng.";
            if (type is not ("group" or "page") || !FacebookUrl(url, type)) return "URL kênh phải là HTTPS Facebook và đúng loại Group/Page.";
            if (!urls.Add(url!.TrimEnd('/'))) return "Không thêm trùng URL kênh trong cùng thương hiệu.";
            if (string.IsNullOrWhiteSpace(channel["name"]?.ToString()) || channel["name"]!.ToString().Length > 200) return "Tên kênh cần từ 1 đến 200 ký tự.";
            if (channel["publishAccountId"] is not null && (channel["publishAccountId"] is not JsonValue accountValue || !accountValue.TryGetValue<string>(out var accountId) || (!string.IsNullOrEmpty(accountId) && !Guid.TryParse(accountId, out _)))) return "ID tài khoản phụ trách kênh không hợp lệ.";
            foreach (var field in new[] { "active", "requireBranding" })
                if (channel[field] is not null && (channel[field] is not JsonValue value || !value.TryGetValue<bool>(out _))) return "Thiết lập kênh không hợp lệ.";
            foreach (var (field, min, max) in new[] { ("dailyTarget", 0, 1000), ("minWords", 1, 3000), ("maxWords", 1, 3000) })
                if (channel[field] is not JsonValue value || !value.TryGetValue<int>(out var number) || number < min || number > max) return $"{field} cần là số nguyên từ {min} đến {max}.";
            if (Number(channel, "minWords", 0) > Number(channel, "maxWords", 0)) return "Độ dài tối thiểu không được lớn hơn tối đa.";
            foreach (var textField in new[] { "audience", "tone", "contentGuidelines" })
                if (channel[textField] is not null && (channel[textField] is not JsonValue textValue || !textValue.TryGetValue<string>(out var channelText) || channelText.Length > 5000)) return "Hướng dẫn riêng của kênh tối đa 5.000 ký tự mỗi trường.";
            if (channel["imageRequirements"] is not null && (channel["imageRequirements"] is not JsonValue imageValue || !imageValue.TryGetValue<string>(out var imageText) || imageText.Length > 5000)) return "Yêu cầu hình ảnh cần là văn bản tối đa 5.000 ký tự.";
        }
        profile["facebookGroupUrl"] = channels.FirstOrDefault(c => c?["type"]?.ToString() == "group" && Flag(c, "active"))?["url"]?.ToString();
        return null;
    }
    public static async Task<string?> ValidatePublishers(JsonObject profile, IProjectRepository db)
    {
        foreach (var channel in Channels(profile))
        {
            var id = channel?["publishAccountId"]?.ToString();
            if (string.IsNullOrEmpty(id)) continue;
            if (!Guid.TryParse(id, out var parsed)) return "ID tài khoản phụ trách không hợp lệ.";
            var record = await db.FindDocumentAsync(parsed, "accounts");
            var account = record is null ? null : JsonNode.Parse(record.Json);
            if (account?["role"]?.ToString() != "publish" || account?["enabled"] is JsonValue enabled && enabled.TryGetValue<bool>(out var active) && !active)
                return "Tài khoản phụ trách phải có vai trò đăng và đang được bật.";
        }
        return null;
    }
    public static JsonNode? SelectChannel(JsonNode profile, string? id) => Channels(profile).FirstOrDefault(channel => channel?["id"]?.ToString() == id && Flag(channel, "active"));
    public static int WordCount(string text) => Regex.Matches(text.Trim(), @"\S+").Count;
    public static string Instructions(JsonNode profile, JsonNode channel)
    {
        string Resolve(string field) => !string.IsNullOrWhiteSpace(channel[field]?.ToString()) ? channel[field]!.ToString() : profile[field]?.ToString() ?? "";
        var writing = new JsonObject();
        foreach (var field in new[] { "name", "tone", "audience", "writingStyle", "callToAction", "requiredKeywords", "forbiddenKeywords", "forbiddenContent", "rules" })
            writing[field] = Resolve(field);
        var tone = Resolve("tone");
        var techVoice = tone.Contains("tech", StringComparison.OrdinalIgnoreCase)
            ? "Với giọng cool tech: được dùng ẩn dụ lập trình nhẹ như compile mong ước, debug nỗi lo, chọn team/full-stack để tạo nét vui. Đây chỉ là cách nói; không đổi các lựa chọn trong ảnh thành món ăn/dịch vụ khác, không chèn quảng cáo công nghệ. Dùng từ tech dễ hiểu; tối đa 2–3 cách ví von công nghệ cho cả bài, không nhồi thuật ngữ."
            : "Chọn cách ví von và từ vựng hợp giọng kênh, không tự áp phong cách công nghệ vào kênh khác.";
        return $"KÊNH ĐÍCH: {Resolve("name")}. GIỌNG VĂN BẮT BUỘC: {tone}. ĐỘC GIẢ: {Resolve("audience")}.\n"
            + $"Viết tiếng Việt, mục tiêu {Number(channel, "minWords", 80)}–{Number(channel, "maxWords", 250)} từ. Không chỉ tóm tắt thành một câu. Mở đầu bằng hook gợi tương tác; chia đoạn ngắn; dùng emoji phù hợp; nếu có các lựa chọn trong ảnh, trình bày từng lựa chọn dễ đọc; cuối bài có CTA, giữ hashtag nguồn hợp lệ và có thể thêm 1–2 hashtag sát chủ đề. Nhắc tên thương hiệu ngắn gọn như trong hashtag nguồn; không cần chép nguyên tiêu đề dài của kênh, không đổi sang thương hiệu mẹ.\n"
            + techVoice + "\n"
            + "Ưu tiên đồng thời: (1) đúng dữ kiện nguồn và ghi chú ảnh đã xác nhận; (2) đúng giọng và cấu trúc kênh. Được sáng tạo cách diễn đạt, ví von và câu hỏi; không sáng tạo dữ kiện, lý do giải nghĩa, sự kiện hay xu hướng. Không bịa để kéo đủ từ. Không khẳng định đã xác minh nguồn hoặc có quyền media.\n"
            + "Quy chuẩn biên tập bổ sung: " + writing.ToJsonString()
            + "\nChỉ trả bài Facebook hoàn chỉnh dạng văn bản thuần. Không dấu sao in đậm, không backtick hoặc đoạn mã; vẫn dùng emoji, xuống dòng, dấu ngoặc kép thông thường và hashtag. Không nhắc ghi chú, OCR, bước xác nhận hay quy trình nội bộ trong bài đăng.";
    }

    public static string? LocalMedia(string root, string? url)
    {
        if (url is null || !url.StartsWith("/media/", StringComparison.Ordinal) || url.Contains('%') || url.Contains('\\')) return null;
        var prefix = Path.GetFullPath(Path.Combine(root, "wwwroot", "media")) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(prefix, url[7..]));
        return path.StartsWith(prefix, StringComparison.Ordinal) && File.Exists(path) && new FileInfo(path).Length > 0 ? path : null;
    }
}
