using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public interface IWorkflowsService
{
    Task<ServiceResult> StartCrawl(JsonObject config);
    Task<ServiceResult> Preflight(Guid id);
    Task<ServiceResult> CreateRun(Guid id);
}

public sealed class WorkflowsService(IProjectRepository db, AiRunner ai) : IWorkflowsService
{
    public async Task<ServiceResult> StartCrawl(JsonObject config)
    {
        if (!Guid.TryParse(config["brandId"]?.ToString(), out var brandId)) return ServiceResult.BadRequest(new { error = "Chọn thương hiệu trước khi chạy." });
        var brand = await db.FindDocumentAsync(brandId, "brands");
        if (brand is null || JsonNode.Parse(brand.Json)?["deletedAt"] is not null) return ServiceResult.BadRequest(new { error = "Thương hiệu không còn hoạt động." });
        if (!Uri.TryCreate(config["url"]?.ToString(), UriKind.Absolute, out var url) || url.Scheme != "https" || !(url.Host == "facebook.com" || url.Host.EndsWith(".facebook.com"))) return ServiceResult.BadRequest(new { error = "Nhập URL Page Facebook hợp lệ." });
        foreach (var (field, min, max) in new (string, double, double)[] { ("maxPosts", 1, 1000), ("maxScrolls", 1, 2000), ("delayMinMs", 500, 120000), ("delayMaxMs", 500, 120000), ("minLikes", 0, 10000000), ("minComments", 0, 10000000), ("accountPostLimit", 1, 1000), ("accountRestMinMs", 1000, 3600000), ("accountRestMaxMs", 1000, 3600000) })
            if (config[field] is not JsonValue v || !v.TryGetValue<double>(out var n) || n < min || n > max || n != Math.Truncate(n)) return ServiceResult.BadRequest(new { error = $"Thông số {field} không hợp lệ." });
        if (config["delayMinMs"]!.GetValue<double>() > config["delayMaxMs"]!.GetValue<double>() || config["accountRestMinMs"]!.GetValue<double>() > config["accountRestMaxMs"]!.GetValue<double>()) return ServiceResult.BadRequest(new { error = "Khoảng nghỉ min không được lớn hơn max." });
        var accounts = (await db.ListDocumentsAsync("accounts")).Where(d => { var a = JsonNode.Parse(d.Json); return a?["role"]?.ToString() == "crawl" && a?["enabled"]?.GetValue<bool>() != false && a?["facebookStatus"]?.ToString() is not ("locked" or "checkpoint" or "expired" or "restricted" or "invalid_cookie"); }).ToList();
        if (accounts.Count == 0) return ServiceResult.BadRequest(new { error = "Không có tài khoản cào đủ điều kiện. Kiểm tra tài khoản đang bật và không bị khóa, hết phiên hoặc giới hạn." });
        var missing = accounts.Count(a => string.IsNullOrEmpty(a.ProtectedSecret));
        if (missing > 0) return ServiceResult.BadRequest(new { error = $"Có {missing} tài khoản cào đang bật nhưng chưa có phiên đăng nhập. Cập nhật phiên hoặc tạm ngưng tài khoản đó." });
        config["crawlAccountIds"] = new JsonArray(accounts.Select(a => JsonValue.Create(a.Id.ToString())).ToArray()); config["extractMedia"] = true; config["targetType"] = "page";
        db.AddLog(new Audit { Message = $"Chưa khởi chạy cào thương hiệu {brandId}: worker Facebook chưa triển khai; {accounts.Count} tài khoản được chọn tự động." }); await db.SaveChangesAsync();
        return ServiceResult.Conflict(new { error = $"Chưa thể chạy: worker cào Facebook, tải ảnh/video và Clef lọc nền chưa được triển khai. Đã xác định {accounts.Count} tài khoản cào đang bật; chưa cào hoặc đăng bài nào." });
    }

    public async Task<ServiceResult> Preflight(Guid id)
    {
        var workflow = await db.FindDocumentAsync(id, "presets");
        if (workflow is null) return ServiceResult.NotFound(); var snapshot = JsonNode.Parse(workflow.Json)!.AsObject();
        if (snapshot["deletedAt"] is not null) return ServiceResult.Conflict(new { error = "Workflow đã nằm trong thùng rác." });
        var checks = await CheckWorkflow(snapshot, db, ai); return ServiceResult.Ok(new { ready = checks.All(c => c.Status == "ok"), checks });
    }

    public async Task<ServiceResult> CreateRun(Guid id)
    {
        var workflow = await db.FindDocumentAsync(id, "presets");
        if (workflow is null) return ServiceResult.NotFound(); var snapshot = JsonNode.Parse(workflow.Json)!.AsObject();
        if (snapshot["deletedAt"] is not null) return ServiceResult.Conflict(new { error = "Workflow đã nằm trong thùng rác." });
        var checks = await CheckWorkflow(snapshot, db, ai); var runId = Guid.NewGuid();
        var result = new { id = runId, workflowId = id, workflowName = snapshot["name"]?.ToString() ?? "Workflow", status = "blocked", createdAt = DateTimeOffset.UtcNow, issues = checks.Where(c => c.Status != "ok").Select(c => c.Detail).ToArray(), checks, snapshot, stages = new[] { new { stage = "crawl", status = "not_started" }, new { stage = "filter", status = "not_started" }, new { stage = "rewrite", status = "not_started" }, new { stage = "publish", status = "not_started" } } };
        db.AddDocument(new Document { Id = runId, Kind = "runs", Json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        db.AddLog(new Audit { Message = $"Lần chạy {runId} bị chặn trước khi thực thi · workflow {id}" }); await db.SaveChangesAsync(); return ServiceResult.Ok(result);
    }
    static async Task<List<WorkflowCheck>> CheckWorkflow(JsonObject config, IProjectRepository db, AiRunner ai)
    {
        var checks = new List<WorkflowCheck>();
        bool FbUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var u) && u.Scheme == "https" && (u.Host == "facebook.com" || u.Host.EndsWith(".facebook.com"));
        checks.Add(new("Page nguồn", FbUrl(config["url"]?.ToString()) ? "ok" : "blocked", FbUrl(config["url"]?.ToString()) ? "Định dạng URL nguồn hợp lệ; chưa xác minh đây là Page truy cập được." : "Thiếu URL Page nguồn hợp lệ."));
        var ids = config["crawlAccountIds"] as JsonArray; var crawlReady = ids is { Count: > 0 };
        foreach (var idNode in ids ?? new JsonArray())
        {
            if (!Guid.TryParse(idNode?.ToString(), out var id)) { crawlReady = false; continue; }
            var account = await db.FindDocumentAsync(id, "accounts"); var data = account is null ? null : JsonNode.Parse(account.Json);
            if (account?.ProtectedSecret is null || data?["role"]?.ToString() != "crawl" || data?["enabled"]?.GetValue<bool>() == false) crawlReady = false;
        }
        checks.Add(new("Tài khoản cào", crawlReady ? "unverified" : "blocked", crawlReady ? "Có phiên đã lưu nhưng chưa kiểm tra hạn phiên hoặc checkpoint Facebook." : "Chọn tài khoản cào được bật và có phiên đăng nhập đã lưu."));
        Document? publisher = null; if (Guid.TryParse(config["publishAccountId"]?.ToString(), out var publishId)) publisher = await db.FindDocumentAsync(publishId, "accounts");
        var pub = publisher is null ? null : JsonNode.Parse(publisher.Json); var pubReady = publisher?.ProtectedSecret is not null && pub?["role"]?.ToString() == "publish" && pub?["enabled"]?.GetValue<bool>() != false;
        checks.Add(new("Tài khoản đăng", pubReady ? "unverified" : "blocked", pubReady ? "Phiên tài khoản đăng chưa được xác minh." : "Chọn tài khoản đăng được bật và có phiên đã lưu."));
        checks.Add(new("Page đích", FbUrl(config["destinationPageUrl"]?.ToString()) ? "unverified" : "blocked", FbUrl(config["destinationPageUrl"]?.ToString()) ? "Có URL Page đích, chưa xác minh quyền đăng." : "Thiếu URL Page đích hợp lệ."));
        Document? brand = null; if (Guid.TryParse(config["brandId"]?.ToString(), out var brandId)) brand = await db.FindDocumentAsync(brandId, "brands");
        var brandReady = brand is not null && JsonNode.Parse(brand.Json)?["deletedAt"] is null; checks.Add(new("Thương hiệu", brandReady ? "ok" : "blocked", brandReady ? "Đã chọn hồ sơ thương hiệu." : "Chưa chọn thương hiệu đang sử dụng."));
        var status = JsonSerializer.SerializeToNode(await ai.Status())!; var models = status["models"]?.AsArray(); var aiReady = status["online"]?.GetValue<bool>() == true && models?.Any(m => m?.ToString() == "clef-flash:latest") == true && (config["branch"]?.ToString() == "repost" || models.Any(m => m?.ToString() == "bonsai-2:27b"));
        checks.Add(new("Ollama và model", aiReady ? "ok" : "blocked", aiReady ? "Ollama kết nối được và có model cần dùng; không chạy suy luận khi kiểm tra." : "Ollama không kết nối được hoặc thiếu Clef/Bonsai."));
        checks.Add(new("Lưu trữ media", "blocked", "Chưa cấu hình kho media và kiểm tra dung lượng; chưa thể tải/kiểm chứng ảnh video gốc."));
        checks.Add(new("Worker thực thi", "blocked", "Worker cào, trích media, Clef lọc và đăng Facebook chưa triển khai. Không tác vụ nào được thực thi."));
        return checks;
    }
}
