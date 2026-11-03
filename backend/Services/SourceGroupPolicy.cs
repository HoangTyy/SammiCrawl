using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SammiCrawl.Backend.Services;

public static class SourceGroupPolicy
{
    static string Plain(string value)=>new string(value.Normalize(System.Text.NormalizationForm.FormD).Where(c=>System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)!=System.Globalization.UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant().Replace('đ','d');
    public static int SearchScore(string name,string keyword)
    {
        if(!MatchesSearch(name,keyword))return -1;
        var title=Plain(name);
        return (Regex.IsMatch(title,@"review|riviu|rivu|danh gia|trai nghiem|tu van|hoi dap|thao luan|so sanh")?6:0)
            +(Regex.IsMatch(title,@"hoc sinh|sinh vien|tiet kiem|gia re")?2:0)
            -(Regex.IsMatch(title,@"cau lac bo|clb|giao luu|offline")?2:0);
    }
    public static string[] DiverseKeywords(IEnumerable<string> words)
    {
        string Topic(string word){var p=Plain(word);return p.Contains("sach")?"books":Regex.IsMatch(p,@"cong nghe|dien thoai|laptop|thiet bi")?"tech":Regex.IsMatch(p,@"hoc tap|van phong pham")?"study":Regex.IsMatch(p,@"sinh hoat|do dung|ky tuc|ktx")?"living":p;}
        var buckets=words.Distinct(StringComparer.OrdinalIgnoreCase).GroupBy(Topic).Select(g=>new Queue<string>(g)).ToArray();var result=new List<string>();
        while(buckets.Any(q=>q.Count>0))foreach(var q in buckets)if(q.Count>0)result.Add(q.Dequeue());
        return result.Take(6).ToArray();
    }
    public static string? CanonicalUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var url) || url.Scheme != "https" || url.Port != 443
            || url.UserInfo.Length > 0 || !(url.Host == "facebook.com" || url.Host.EndsWith(".facebook.com"))) return null;
        var match = Regex.Match(url.AbsolutePath, @"^/groups/([A-Za-z0-9._-]+)/?$", RegexOptions.CultureInvariant);
        if (!match.Success || new[] { "feed", "discover", "search", "joins", "create" }.Contains(match.Groups[1].Value.ToLowerInvariant())) return null;
        return "https://www.facebook.com/groups/" + match.Groups[1].Value.ToLowerInvariant() + "/";
    }

    public static bool MatchesSearch(string name,string keyword)
    {
        var title=Plain(name);var query=Plain(keyword);
        var learningQuery=Regex.IsMatch(query,@"review|sach|do dung|meo|so sanh|hoc tap|cong nghe|kinh nghiem");
        if(learningQuery&&Regex.IsMatch(title,@"mua\s*(?:&|va)?\s*ban|thanh ly|cho thue|phong tro|si le|bo si|in hinh|dat hang|nha phan phoi|my pham|skincare|lam dep|phong kham"))return false;
        if(query.Contains("cong nghe")&&title.Contains("sinh hoc")&&!query.Contains("sinh hoc"))return false;
        if(query.Contains("sach"))return title.Contains("sach");
        if(query.Contains("ktx")||query.Contains("ky tuc"))return title.Contains("ktx")||title.Contains("ky tuc")||title.Contains("do dung");
        if(query.Contains("do dung hoc tap"))return title.Contains("hoc tap")||title.Contains("van phong pham")||title.Contains("do dung");
        var terms=Regex.Matches(query,@"[a-z0-9]+").Select(m=>m.Value).Where(w=>!new[]{"sinh","vien","hoc","nhom","hoi","cong","dong","kinh","nghiem","cho","ve","cac","gia","phai","chang"}.Contains(w)).Distinct().ToArray();
        if(terms.Length==0)return false;
        if(query.Contains("review")&&!Regex.IsMatch(title,@"review|rivu|riviu|danh gia"))return false;
        return terms.Count(w=>title.Contains(w,StringComparison.Ordinal))>=Math.Max(1,(int)Math.Ceiling(terms.Length*.6));
    }
    public static bool PoorSource(int evaluated,int rejected,int recommended)=>evaluated>=5&&recommended==0&&rejected*5>=evaluated*4;
    public static int SourceQuota(int total,int sources)=>sources<=1?total:Math.Max(1,(int)Math.Ceiling((double)total/sources));
    public static bool Truncated(string text)=>Regex.IsMatch(text.Trim(),@"(?:Xem thêm|See more)\s*$",RegexOptions.IgnoreCase);
    public static bool ExpiredInvitation(string text,DateOnly today)
    {
        if(!Regex.IsMatch(text,@"đăng ký|mời.{0,35}(?:tham gia|offline)|hẹn gặp|sắp diễn ra",RegexOptions.IgnoreCase))return false;
        var dates=Regex.Matches(text,@"(?<!\d)(\d{1,2})[/-](\d{1,2})[/-](20\d{2})(?!\d)").Select(m=>DateOnly.TryParseExact($"{m.Groups[1].Value.PadLeft(2,'0')}/{m.Groups[2].Value.PadLeft(2,'0')}/{m.Groups[3].Value}","dd/MM/yyyy",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var d)?(DateOnly?)d:null).Where(d=>d.HasValue).Select(d=>d!.Value).ToArray();
        return dates.Length>0&&dates.All(d=>d<today);
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
