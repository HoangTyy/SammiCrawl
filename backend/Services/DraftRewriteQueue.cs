using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;

namespace SammiCrawl.Backend.Services;

// Uses the same FIFO as gateway work. Each job owns its DI scope until the draft is saved.
public sealed class DraftRewriteQueue(AiGatewayQueue queue, IServiceScopeFactory scopes)
{
    readonly ConcurrentDictionary<Guid, (Guid ArticleId, string ChannelId)> jobs = new();
    public object Submit(Guid id, Guid articleId, string channelId)
    {
        foreach (var existing in jobs.Keys) if (queue.Find(existing) is null) jobs.TryRemove(existing, out _);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{articleId}:{channelId}")));
        var snapshot = queue.Enqueue(id, "draft-rewrite", fingerprint, Encoding.UTF8.GetByteCount(channelId) + 128, async () =>
        {
            using var scope = scopes.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IContentService>().CreateDraft(articleId,
                new JsonObject { ["branch"] = "rewrite", ["channelId"] = channelId });
            if (result.Status != 200 || result.Value is not JsonObject draft)
            {
                var error = System.Text.Json.JsonSerializer.SerializeToNode(result.Value);
                throw new AiGatewayException(error?["code"]?.ToString() ?? "REWRITE_FAILED",
                    error?["error"]?.ToString() ?? "Chưa tạo được bản nháp viết lại.", result.Status);
            }
            return draft;
        });
        jobs.TryAdd(id, (articleId, channelId));
        return View(snapshot, articleId, channelId);
    }
    static object View(AiJobSnapshot snapshot, Guid articleId, string channelId) => new {
        snapshot.Id, articleId, channelId, snapshot.State, snapshot.QueuePosition,
        snapshot.CreatedAt, snapshot.StartedAt, snapshot.FinishedAt, snapshot.Result, snapshot.Failure
    };
    public object? Find(Guid id)
    {
        if (!jobs.TryGetValue(id, out var meta)) return null;
        var snapshot = queue.Find(id);
        if (snapshot is not null) return View(snapshot, meta.ArticleId, meta.ChannelId);
        jobs.TryRemove(id, out _); return null;
    }
    public object[] List() => jobs.Keys.Select(Find).Where(j => j is not null).Cast<object>().ToArray();
    public bool Cancel(Guid id) => jobs.ContainsKey(id) && queue.CancelQueued(id);
}
