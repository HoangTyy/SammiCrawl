using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public sealed class CrawlEvaluationState
{
    public static string? ReadinessError(JsonNode? status)
    {
        if (status?["online"]?.GetValue<bool>() != true) return "Không kết nối được AI để đánh giá Clef. Kiểm tra Ollama hoặc gateway trước khi cào.";
        if (status["models"] is not JsonArray models || !models.Any(model => model?.ToString() == "clef-flash:latest"))
            return "Máy AI chưa có clef-flash:latest. Cần cài đúng model và endpoint /v1/systemone trước khi cào và đánh giá.";
        return null;
    }

    public int Evaluated { get; private set; }
    public int Recommended { get; private set; }
    public int Review { get; private set; }
    public int Rejected { get; private set; }

    public bool Record(ServiceResult result)
    {
        if (result.Status != 200 || result.Value is not JsonObject article
            || !DateTimeOffset.TryParse(article["aiEvaluatedAt"]?.ToString(), out _)
            || !double.TryParse(article["aiScore"]?.ToString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var score) || !double.IsFinite(score) || score < 0 || score > 100)
            return false;
        if (article["channelEvaluationRequired"]?.GetValue<bool>() == true && article["aiChannelEvaluationStatus"]?.ToString() != "complete") return false;
        switch (article["aiStatus"]?.ToString())
        {
            case "recommended": Recommended++; break;
            case "review": Review++; break;
            case "rejected": Rejected++; break;
            default: return false;
        }
        Evaluated++;
        return true;
    }

    public void WriteTo(JsonObject task)
    {
        task["clefEvaluatedPosts"] = Evaluated;
        task["clefRecommendedPosts"] = Recommended;
        task["clefReviewPosts"] = Review;
        task["clefRejectedPosts"] = Rejected;
    }

    public static int Progress(string status, int captured, int target, int evaluated)
    {
        if (target <= 0) return 0;
        if (status == "completed" && captured >= target && evaluated >= captured) return 100;
        return Math.Clamp((int)((90L * captured + 10L * Math.Min(captured, evaluated)) / target), 0, 99);
    }

    public static void ExcludeRejected(JsonObject article)
    {
        if (article["aiStatus"]?.ToString() != "rejected") return;
        article["deletedAt"] = DateTimeOffset.UtcNow.ToString("O");
        article["deletedReason"] = "clef_rejected";
    }

    public static bool RepairIncompleteCompletion(JsonObject task, IReadOnlyList<JsonObject> articles)
    {
        if (task["status"]?.ToString() != "completed" || articles.Count == 0) return false;
        var evaluation = new CrawlEvaluationState();
        foreach (var article in articles) evaluation.Record(ServiceResult.Ok(article));
        if (evaluation.Evaluated == articles.Count) return false;
        evaluation.WriteTo(task);
        task["status"] = "paused";
        task["errorCode"] = "CLEF_EVALUATION_INCOMPLETE";
        task["errorReason"] = $"Tác vụ cũ báo hoàn thành khi Clef mới đánh giá {evaluation.Evaluated}/{articles.Count} bài. Cần đánh giá lại các bài chờ; không tự chạy lại cào Facebook.";
        task["progress"] = Progress("paused", task["crawledPosts"]?.GetValue<int>() ?? articles.Count,
            task["maxPosts"]?.GetValue<int>() ?? articles.Count, evaluation.Evaluated);
        return true;
    }
}
