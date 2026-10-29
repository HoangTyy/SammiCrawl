using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public sealed class AiModelUnavailableException(string message) : Exception(message)
{
    public static string? CheckBonsai(JsonNode? status)
    {
        if (status?["online"]?.GetValue<bool>() != true)
            return "Không kết nối được máy AI. Kiểm tra Ollama hoặc gateway trước khi đọc ảnh/rewrite.";
        if (status["models"] is not JsonArray models || !models.Any(model => model?.ToString() == "bonsai-2:27b"))
            return "Máy AI chưa có bonsai-2:27b để đọc ảnh và rewrite. Cài đúng model hoặc cấu hình gateway có model này; bonsai-2:9b không tự thay thế model đã cấu hình.";
        return null;
    }
}
