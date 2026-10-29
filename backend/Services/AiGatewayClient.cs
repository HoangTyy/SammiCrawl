using System.Text.Json;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public static class AiGatewayClient
{
    public static async Task<JsonObject> PostAsync(HttpClient client, string path, object payload)
    {
        // One retry only for a transient response to stateless evaluation.
        for (var attempt = 0; ; attempt++)
        {
            using var response = await client.PostAsJsonAsync(path, payload);
            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                try { return (JsonNode.Parse(body) as JsonObject) ?? throw AiGatewayException.InvalidOutput(); }
                catch (JsonException) { throw AiGatewayException.InvalidOutput(); }
            }
            string? code = null;
            if (body.Length < 65536)
                try { code = (JsonNode.Parse(body) as JsonObject)?["code"]?.ToString(); } catch (JsonException) { }
            var failure = AiGatewayException.FromResponse((int)response.StatusCode, code);
            if (path == "bridge/evaluate" && attempt == 0 && failure.Retryable)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                continue;
            }
            throw failure;
        }
    }
}
