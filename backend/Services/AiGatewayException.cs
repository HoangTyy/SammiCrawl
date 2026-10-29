using System.Net;
using System.Text.Json;

namespace SammiCrawl.Backend.Services;

public sealed class AiGatewayException(string code, string message, int status = 502) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    public bool Retryable => Code == "AI_GATEWAY_TRANSIENT";

    public static AiGatewayException FromResponse(int status, string? code) => code switch
    {
        "CLEF_INVALID_OUTPUT" => InvalidOutput(),
        "AI_MODEL_UNAVAILABLE" => new(code, "Máy chủ AI thiếu model hoặc chưa sẵn sàng.", 503),
        "AI_UNLOAD_FAILED" => new(code, "Máy chủ AI chưa giải phóng được model. Kiểm tra gateway.", 502),
        "AI_UPSTREAM_FAILED" => new(code, "Gateway không hoàn tất gọi Ollama. Kiểm tra log và model trên máy AI."),
        "AI_TIMEOUT" => new(code, "Máy chủ AI xử lý quá thời gian; chưa nhận được kết quả hợp lệ.", 504),
        "AI_GATEWAY_FAILED" => new(code, "Clef chưa hoàn tất đánh giá. Kiểm tra log gateway theo mã lỗi."),
        _ => status switch
        {
            409 => new("AI_BUSY", "Máy chủ AI đang bận. Chờ tác vụ hiện tại hoàn tất rồi thử lại.", 409),
            429 => new("AI_RATE_LIMITED", "Gateway AI đang giới hạn yêu cầu. Chờ rồi thử lại.", 429),
            401 or 403 => new("AI_GATEWAY_AUTH", "Gateway AI từ chối truy cập. Kiểm tra khóa và chính sách Cloudflare.", 502),
            404 => new("AI_GATEWAY_ENDPOINT", "Gateway AI chưa có endpoint cần thiết. Cập nhật gateway trên máy chủ AI.", 502),
            502 or 504 or 520 or 522 or 524 => new("AI_GATEWAY_TRANSIENT", $"Gateway AI trả HTTP {status}. Chưa có kết quả đánh giá hợp lệ; kiểm tra kết nối/gateway trên máy AI.", 502),
            _ => new("AI_GATEWAY_FAILED", $"Máy chủ AI trả HTTP {status}; chưa hoàn tất đánh giá.", 502)
        }
    };

    public static AiGatewayException InvalidOutput() => new("CLEF_INVALID_OUTPUT", "Clef trả kết quả thiếu hoặc không hợp lệ. Cần kiểm tra log gateway; không dùng kết quả này để lọc bài.");
    public static AiGatewayException FromException(Exception error) => error switch
    {
        AiGatewayException known => known,
        JsonException => InvalidOutput(),
        OperationCanceledException => new("AI_TIMEOUT", "Máy chủ AI xử lý quá thời gian. Chưa nhận được kết quả đánh giá hợp lệ.", 504),
        HttpRequestException http => new("AI_UPSTREAM_FAILED", http.StatusCode is HttpStatusCode status ? $"Ollama trên máy AI trả HTTP {(int)status}. Kiểm tra dịch vụ/model trên máy AI." : "Gateway không kết nối được Ollama trên máy AI."),
        _ => new("AI_GATEWAY_FAILED", "Clef chưa hoàn tất đánh giá. Kiểm tra log gateway theo mã lỗi.")
    };
}
