using System.Text;
using System.Text.RegularExpressions;
namespace SammiCrawl.Backend.Services;

public sealed class UnchangedRewriteException() : Exception("Bonsai trả lại nguyên văn bài gốc. Chưa tạo bản nháp rewrite; vui lòng thử lại.");

public static class RewriteOutput
{
    static string Fingerprint(string text) => Regex.Replace(text.Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"[^\p{L}\p{N}]", "");
    public static string Validate(string source, string output)
    {
        var cleaned = FacebookPlainText.Clean(output);
        if (Fingerprint(source) == Fingerprint(cleaned)) throw new UnchangedRewriteException();
        return cleaned;
    }
}
