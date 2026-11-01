using System.Text.Json.Nodes;
namespace SammiCrawl.Backend.Services;
public sealed class CrawlResumeState
{
    public static bool CanClaim(JsonObject task,string token)=>task["status"]?.ToString()=="pending"&&task["executionToken"]?.ToString()==token;
    public int Captured { get; }
    public int Duplicates { get; }
    public CrawlEvaluationState Evaluation { get; }=new();
    public List<Guid> PendingEvaluation { get; }=[];
    public Dictionary<string,int> ByAccount { get; }=new();
    public Dictionary<string,int> BySource { get; }=new();
    public HashSet<string> Seen { get; }=[];
    public CrawlResumeState(JsonObject task,IEnumerable<JsonObject> articles)
    {
        var rows=articles.Where(a=>a["crawlTaskId"]?.ToString()==task["id"]?.ToString()).ToList();
        Captured=Math.Max(task["crawledPosts"]?.GetValue<int>()??0,rows.Count);Duplicates=task["duplicatesSkipped"]?.GetValue<int>()??0;
        foreach(var a in rows)
        {
            if(!Evaluation.Record(ServiceResult.Ok(a))&&Guid.TryParse(a["id"]?.ToString(),out var id))PendingEvaluation.Add(id);
            if(a["sourceAccountId"]?.ToString() is { } account)ByAccount[account]=ByAccount.GetValueOrDefault(account)+1;
            var source=a["crawlSourceUrl"]?.ToString()??a["sourceGroupUrl"]?.ToString()??(task["sourceMode"]?.ToString()=="url"?task["url"]?.ToString():null);
            if(source is not null)BySource[source]=BySource.GetValueOrDefault(source)+1;
            if(ArticleDeduplication.SourceKey(a["sourceUrl"]?.ToString()??"") is { } key)Seen.Add(key);
        }
    }
}
