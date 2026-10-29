using Microsoft.EntityFrameworkCore;

namespace SammiCrawl.Backend.Repositories;

// One scoped repository shares the EF unit of work across services in each request.
public sealed class ProjectRepository(Store store, LiveUpdates liveUpdates) : IProjectRepository
{
    public async Task<(Document Record, bool Duplicate, string? Reason)> IngestArticleAsync(Guid brandId, System.Text.Json.Nodes.JsonObject article)
    {
        await using var tx = await store.Database.BeginTransactionAsync();
        // Database lock spans lookup + insert and works across application replicas.
        var resource = "article-ingest:" + brandId;
        if (store.Database.IsNpgsql()) await store.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({resource}, 0))");
        else await store.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=30000; IF @r < 0 THROW 50001, 'Article lock unavailable', 1;");
        var records = await store.Documents.Where(d => d.Kind == "articles").AsNoTracking().ToListAsync();
        foreach (var record in records)
        {
            var old = System.Text.Json.Nodes.JsonNode.Parse(record.Json)!.AsObject();
            if (old["brandId"]?.ToString() != brandId.ToString()) continue;
            var reason = ArticleDeduplication.Match(article, old);
            if (reason is not null) { await tx.CommitAsync(); return (record, true, reason); }
        }
        var id = Guid.NewGuid(); article["id"] = id.ToString(); article["brandId"] = brandId.ToString(); article["createdAt"] = DateTimeOffset.UtcNow.ToString("O");
        article["contentHash"] = ArticleDeduplication.ContentKey(article); article["sourceKey"] = ArticleDeduplication.SourceKey(article["sourceUrl"]?.ToString() ?? "");
        var saved = new Document { Id = id, Kind = "articles", Json = article.ToJsonString() };
        store.Documents.Add(saved); await store.SaveChangesAsync(); await tx.CommitAsync(); liveUpdates.Notify(); return (saved, false, null);
    }

    public Task<Document?> FindDocumentAsync(Guid id, string kind) => store.Documents.SingleOrDefaultAsync(d => d.Id == id && d.Kind == kind);
    public Task<Document?> FindJobAsync(Guid id) => store.Documents.SingleOrDefaultAsync(d => d.Id == id && (d.Kind == "tasks" || d.Kind == "runs"));
    public Task<List<Document>> ListDocumentsAsync(string kind) => store.Documents.Where(d => d.Kind == kind).OrderByDescending(d => d.CreatedAt).ToListAsync();
    public Task<ProjectUser?> FindUserByEmailAsync(string email) => store.Users.SingleOrDefaultAsync(user => user.Email == email);
    public Task<bool> HasUsersAsync() => store.Users.AnyAsync();
    public Task<List<Audit>> RecentLogsAsync(int count) => store.Logs.OrderByDescending(log => log.Id).Take(count).ToListAsync();
    public string? ProviderName => store.Database.ProviderName;
    public Task<bool> CanConnectAsync() => store.Database.CanConnectAsync();
    public void AddDocument(Document document) => store.Documents.Add(document);
    public void AddUser(ProjectUser user) => store.Users.Add(user);
    public void AddLog(Audit audit) => store.Logs.Add(audit);
    public Task ReloadAsync(Document document) => store.Entry(document).ReloadAsync();
    public async Task<int> SaveChangesAsync() { var count = await store.SaveChangesAsync(); if (count > 0) liveUpdates.Notify(); return count; }
    public async Task<bool> TryUpdateBrandAsync(Guid id, string expectedJson, string updatedJson, string auditMessage)
    {
        await using var transaction = await store.Database.BeginTransactionAsync();
        var changed = await store.Documents.Where(d => d.Id == id && d.Kind == "brands" && d.Json == expectedJson)
            .ExecuteUpdateAsync(update => update.SetProperty(d => d.Json, updatedJson));
        if (changed == 0) return false;
        store.Logs.Add(new Audit { Message = auditMessage });
        await store.SaveChangesAsync();
        await transaction.CommitAsync();
        liveUpdates.Notify();
        return true;
    }
}
