using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public sealed class SourceGroupsService(IProjectRepository db)
{
    public async Task<ServiceResult> List()
    {
        var articles = (await db.ListDocumentsAsync("articles")).Select(d => JsonNode.Parse(d.Json)!).ToList();
        var groups = (await db.ListDocumentsAsync("sourceGroups")).Select(d => JsonNode.Parse(d.Json)!.AsObject()).ToList();
        foreach (var group in groups)
        {
            var own = articles.Where(a => a["sourceGroupId"]?.ToString() == group["id"]?.ToString()).ToList();
            group["collectedPosts"] = own.Count;
            group["acceptedPosts"] = own.Count(SourceGroupPolicy.Accepted);
            group["reviewPosts"] = own.Count(a => a["aiStatus"]?.ToString() == "review" && a["deletedAt"] is null);
            group["rejectedPosts"] = own.Count(a => a["aiStatus"]?.ToString() == "rejected");
            group["pendingPosts"] = own.Count(a => a["aiEvaluatedAt"] is null || a["aiChannelEvaluationStatus"]?.ToString() == "pending");
            var channels = own.Where(SourceGroupPolicy.Accepted).SelectMany(a => (a["aiChannelMatches"] as JsonArray ?? []).Where(c => c?["status"]?.ToString() == "recommended"))
                .GroupBy(c => c!["channelId"]!.ToString()).Select(g => new JsonObject { ["channelId"] = g.Key, ["name"] = g.First()!["name"]?.ToString(), ["acceptedPosts"] = g.Count() });
            group["channels"] = new JsonArray(channels.Cast<JsonNode>().ToArray());
        }
        return ServiceResult.Ok(groups);
    }
}
