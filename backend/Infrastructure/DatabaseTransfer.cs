using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SammiCrawl.Backend.Infrastructure;

public static class DatabaseTransfer
{
    public static async Task Run(IConfiguration config)
    {
        var sourceConnection = config["Transfer:SqlServer"] ?? throw new InvalidOperationException("Missing source connection.");
        var targetConnection = config["Transfer:Postgres"] ?? throw new InvalidOperationException("Missing target connection.");
        await using var source = new Store(new DbContextOptionsBuilder<Store>().UseSqlServer(sourceConnection).Options);
        await using var target = new Store(new DbContextOptionsBuilder<Store>().UseNpgsql(targetConnection).Options);
        var documents = await source.Documents.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        var users = await source.Users.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        var logs = await source.Logs.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        DateTimeOffset Normalize(DateTimeOffset value) { var utc = value.ToUniversalTime(); return utc.AddTicks(-(utc.Ticks % 10)); }
        foreach (var d in documents) d.CreatedAt = Normalize(d.CreatedAt);
        foreach (var l in logs) l.CreatedAt = Normalize(l.CreatedAt);
        await target.Database.EnsureCreatedAsync();
        if (await target.Documents.AnyAsync() || await target.Users.AnyAsync() || await target.Logs.AnyAsync()) throw new InvalidOperationException("Target is not empty; refusing to overwrite existing records.");
        await using var transaction = await target.Database.BeginTransactionAsync();
        target.Documents.AddRange(documents); target.Users.AddRange(users); target.Logs.AddRange(logs); await target.SaveChangesAsync();
        await target.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('sammicrawl.\"Logs\"', 'Id'), COALESCE(MAX(\"Id\"), 1), COUNT(*) > 0) FROM sammicrawl.\"Logs\"");
        var copiedDocuments = await target.Documents.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        var copiedUsers = await target.Users.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        var copiedLogs = await target.Logs.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        documents.Sort((a, b) => a.Id.CompareTo(b.Id)); copiedDocuments.Sort((a, b) => a.Id.CompareTo(b.Id));
        users.Sort((a, b) => a.Id.CompareTo(b.Id)); copiedUsers.Sort((a, b) => a.Id.CompareTo(b.Id));
        string Fingerprint<T>(IEnumerable<T> data) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data))));
        var docOk = Fingerprint(documents) == Fingerprint(copiedDocuments);
        var usersOk = Fingerprint(users) == Fingerprint(copiedUsers);
        var logsOk = Fingerprint(logs) == Fingerprint(copiedLogs);
        if (!docOk || !usersOk || !logsOk) throw new InvalidOperationException($"Transfer verification failed: documents={docOk}, users={usersOk}, logs={logsOk}. Target transaction rolled back.");
        await transaction.CommitAsync();
        Console.WriteLine(JsonSerializer.Serialize(new { documents = documents.Count, users = users.Count, logs = logs.Count, verified = docOk && usersOk && logsOk }));
    }
}
