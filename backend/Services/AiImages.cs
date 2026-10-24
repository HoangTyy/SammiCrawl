namespace SammiCrawl.Backend.Services;
public static class AiImages
{
    public static string? Validate(List<string>? images)
    {
        if ((images?.Count ?? 0) > 4) return "Chỉ phân tích tối đa 4 ảnh mỗi bài.";
        foreach (var image in images ?? [])
        {
            if (string.IsNullOrWhiteSpace(image) || image.Length > 7000000) return "Ảnh không hợp lệ hoặc vượt 5 MB.";
            try
            {
                var b = Convert.FromBase64String(image);
                if (b.Length < 12 || b.Length > 5000000 || !((b[0] == 255 && b[1] == 216) || (b[0] == 137 && b[1] == 80 && b[2] == 78 && b[3] == 71) || (System.Text.Encoding.ASCII.GetString(b,0,4) == "RIFF" && System.Text.Encoding.ASCII.GetString(b,8,4) == "WEBP"))) return "Ảnh cần là JPEG/PNG/WebP base64 hợp lệ.";
            }
            catch (FormatException) { return "Ảnh base64 không hợp lệ."; }
        }
        return null;
    }
}
