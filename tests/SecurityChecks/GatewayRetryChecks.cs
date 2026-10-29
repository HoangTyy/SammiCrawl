using System.Net;
using System.Net.Http.Json;
using SammiCrawl.Backend.Services;

public static class GatewayRetryChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        static HttpResponseMessage Reply(int status, object body) => new((HttpStatusCode)status) { Content = JsonContent.Create(body) };
        using var recovering = new FixtureHandler(n => n == 1 ? Reply(502, new { error = "fixture gateway failure" }) : Reply(200, new { status = "rejected", score = 25 }));
        using var client = new HttpClient(recovering) { BaseAddress = new Uri("http://fixture.invalid/") };
        var result = await AiGatewayClient.PostAsync(client, "bridge/evaluate", new { text = "fixture" });
        check(recovering.Calls == 2 && result["status"]?.ToString() == "rejected", "transient evaluate502 retries once and uses real returned result");

        async Task FailsOnce(string path, int status, string? code, string expected, string label, int attempts = 1)
        {
            using var handler = new FixtureHandler(_ => Reply(status, new { error = "fixture", code }));
            using var http = new HttpClient(handler) { BaseAddress = new Uri("http://fixture.invalid/") };
            try { await AiGatewayClient.PostAsync(http, path, new { text = "fixture" }); check(false, label); }
            catch (AiGatewayException error) { check(handler.Calls == attempts && error.Code == expected, label); }
        }
        await FailsOnce("bridge/evaluate", 502, null, "AI_GATEWAY_TRANSIENT", "persistent gateway502 stops after two attempts without fake success", 2);
        await FailsOnce("bridge/evaluate", 409, null, "AI_BUSY", "busy409 never retried or queued");
        await FailsOnce("bridge/evaluate", 429, null, "AI_RATE_LIMITED", "rate limit429 never retried");
        await FailsOnce("bridge/evaluate", 502, "CLEF_INVALID_OUTPUT", "CLEF_INVALID_OUTPUT", "invalid Clef output is never retried or accepted");
        await FailsOnce("bridge/evaluate", 502, "AI_UPSTREAM_FAILED", "AI_UPSTREAM_FAILED", "classified Ollama error preserved without retry");
        await FailsOnce("bridge/rewrite", 502, null, "AI_GATEWAY_TRANSIENT", "rewrite never automatically retried");
        await FailsOnce("bridge/evaluate", 403, null, "AI_GATEWAY_AUTH", "gateway auth failure not confused with project logout");
        check(AiGatewayException.FromException(new System.Text.Json.JsonException()).Code == "CLEF_INVALID_OUTPUT", "gateway classifies malformed JSON without exposing response body");
        check(AiGatewayException.FromException(new TaskCanceledException()).Status == 504, "AI timeout retains actionable classification");
    }

    sealed class FixtureHandler(Func<int, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(reply(++Calls));
    }
}
