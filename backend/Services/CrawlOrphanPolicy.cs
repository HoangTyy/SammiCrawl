using System.Text.Json.Nodes;
namespace SammiCrawl.Backend.Services;
public static class CrawlOrphanPolicy
{
    public static bool Blocks(JsonObject task)=>task["status"]?.ToString()=="running"||(task["status"]?.ToString()=="paused"&&task["pauseAcknowledged"]?.GetValue<bool>()==false);
    // Called only while holding the global DB lock; a live owner prevents entering recovery.
    public static bool Recoverable(JsonObject task,DateTimeOffset databaseNow)=>Blocks(task)&&task["crawlLockProtocol"]?.ToString()=="1"&&DateTimeOffset.TryParse(task["workerHeartbeatAt"]?.ToString(),out var last)&&databaseNow-last>TimeSpan.FromSeconds(90);
}
