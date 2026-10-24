using System.Text.RegularExpressions;

namespace SammiCrawl.Backend.Services;

public static class FacebookPlainText
{
    public static string Clean(string text)
    {
        // Keep readable text, URLs, emoji and hashtags; remove Markdown presentation.
        text = Regex.Replace(text, @"(?m)^\s*```[^\r\n]*\r?\n|^\s*```\s*$", "");
        text = Regex.Replace(text, @"`+([^`]+)`+", "$1");
        text = Regex.Replace(text, @"!\[([^\]]*)\]\(([^)]+)\)", "$1 ($2)");
        text = Regex.Replace(text, @"\[([^\]]+)\]\(([^)]+)\)", "$1 ($2)");
        text = Regex.Replace(text, @"(\*\*|__|~~)(.+?)\1", "$2", RegexOptions.Singleline);
        text = Regex.Replace(text, @"(?<!\w)(\*|_)([^\r\n]+?)\1(?!\w)", "$2");
        text = Regex.Replace(text, @"(?m)^ {0,3}#{1,6} +", "");
        text = Regex.Replace(text, @"(?m)^ {0,3}> ?", "");
        text = Regex.Replace(text, @"(?m)^ {0,3}[-*+] +", "• ");
        text = text.Trim();
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("AI trả nội dung rỗng; chưa tạo bản nháp.");
        return text;
    }
}
