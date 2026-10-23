using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public interface IContentService
{
    Task<ServiceResult> CreateDraft(Guid id, JsonObject request);
    Task<ServiceResult> ApproveDraft(Guid id, JsonObject request);
    Task<ServiceResult> Rewrite(RewriteRequest r);
}

public sealed class ContentService(IProjectRepository db, AiRunner ai, IWebHostEnvironment environment, ILogger<ContentService> logger) : IContentService
{
    public async Task<ServiceResult> CreateDraft(Guid id, JsonObject request)
    {
        var doc = await db.FindDocumentAsync(id, "articles"); if (doc is null) return ServiceResult.NotFound(new { error = "Không tìm thấy bài gốc." });
        var article = JsonNode.Parse(doc.Json)!; var branch = request["branch"]?.ToString(); if (branch is not ("repost" or "rewrite")) return ServiceResult.BadRequest(new { error = "Chọn repost hoặc rewrite." });
        var source = article["content"]?.ToString(); if (string.IsNullOrWhiteSpace(source)) return ServiceResult.BadRequest(new { error = "Bài gốc chưa có nội dung." });
        Document? brand = null; if (Guid.TryParse(article["brandId"]?.ToString(), out var brandId)) brand = await db.FindDocumentAsync(brandId, "brands");
        if (brand is null || JsonNode.Parse(brand.Json)?["deletedAt"] is not null) return ServiceResult.BadRequest(new { error = "Thương hiệu không còn hoạt động." });
        var profile = JsonNode.Parse(brand.Json)!; var content = source;
        if (branch == "rewrite")
        {
            if (source.Length > 20000) return ServiceResult.BadRequest(new { error = "Bài gốc vượt giới hạn 20.000 ký tự." });
            if (!ai.TryAcquire()) return ServiceResult.Conflict(new { error = "AI đang bận. Chỉ chạy một model mỗi lần." });
            try { content = await ai.Rewrite(new RewriteRequest(source, profile["tone"]?.ToString() ?? "Thân thiện", "rewrite", profile.ToJsonString())); }
            catch (Exception ex) { logger.LogError(ex, "Article rewrite failed"); return ServiceResult.Json(new { error = "Bonsai chưa hoàn thành bài viết. Kiểm tra Ollama." }, statusCode: 502); }
            finally { ai.Release(); }
        }
        var draftId = Guid.NewGuid(); var draft = new JsonObject { ["id"] = draftId.ToString(), ["articleId"] = id.ToString(), ["brandId"] = article["brandId"]?.ToString(), ["branch"] = branch, ["content"] = content, ["sourceContent"] = source, ["media"] = article["media"]?.DeepClone() ?? new JsonArray(), ["status"] = "awaiting_review", ["createdAt"] = DateTimeOffset.UtcNow.ToString("O") };
        db.AddDocument(new Document { Id = draftId, Kind = "drafts", Json = draft.ToJsonString() }); db.AddLog(new Audit { Message = $"Đã tạo bản nháp {branch} {draftId} từ bài {id}, chờ duyệt." }); await db.SaveChangesAsync(); return ServiceResult.Ok(draft);
    }

    public async Task<ServiceResult> ApproveDraft(Guid id, JsonObject request)
    {
        var doc = await db.FindDocumentAsync(id, "drafts"); if (doc is null) return ServiceResult.NotFound(); var draft = JsonNode.Parse(doc.Json)!.AsObject();
        if (draft["status"]?.ToString() != "awaiting_review") return ServiceResult.Conflict(new { error = "Bản nháp đã được duyệt; không gửi lại." });
        if (string.IsNullOrWhiteSpace(request["content"]?.ToString())) return ServiceResult.BadRequest(new { error = "Nội dung không được để trống." });
        var media = draft["media"] as JsonArray;
        if (media is null || media.Count == 0) return ServiceResult.Conflict(new { error = "Thiếu ảnh/video gốc. Chưa thể duyệt đăng." });
        var mediaRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot", "media")) + Path.DirectorySeparatorChar;
        foreach (var item in media)
        {
            var path = item?["url"]?.ToString(); if (path is null || !path.StartsWith("/media/", StringComparison.Ordinal) || path.Contains('%') || item?["status"]?.ToString() != "downloaded") return ServiceResult.Conflict(new { error = "Media gốc chưa được tải và kiểm tra đầy đủ." });
            var file = Path.GetFullPath(Path.Combine(mediaRoot, path[7..])); if (!file.StartsWith(mediaRoot, StringComparison.Ordinal) || !File.Exists(file) || new FileInfo(file).Length == 0) return ServiceResult.Conflict(new { error = "File ảnh/video gốc không tồn tại hoặc bị rỗng." });
        }
        Document? brand = null; if (Guid.TryParse(draft["brandId"]?.ToString(), out var brandId)) brand = await db.FindDocumentAsync(brandId, "brands"); var profile = brand is null ? null : JsonNode.Parse(brand.Json);
        if (profile is null || profile["deletedAt"] is not null) return ServiceResult.BadRequest(new { error = "Thương hiệu không còn hoạt động." });
        var group = profile["facebookGroupUrl"]?.ToString(); if (!Uri.TryCreate(group, UriKind.Absolute, out var groupUrl) || groupUrl.Scheme != "https" || !(groupUrl.Host == "facebook.com" || groupUrl.Host.EndsWith(".facebook.com")) || !groupUrl.AbsolutePath.StartsWith("/groups/")) return ServiceResult.BadRequest(new { error = "Cấu hình URL nhóm Facebook đích trong hồ sơ thương hiệu." });
        if (!Guid.TryParse(request["publishAccountId"]?.ToString(), out var publishId)) return ServiceResult.BadRequest(new { error = "Chọn tài khoản đăng." });
        var publisher = await db.FindDocumentAsync(publishId, "accounts"); var account = publisher is null ? null : JsonNode.Parse(publisher.Json);
        if (publisher?.ProtectedSecret is null || account?["role"]?.ToString() != "publish" || account?["enabled"]?.GetValue<bool>() == false) return ServiceResult.BadRequest(new { error = "Tài khoản đăng cần đúng vai trò, đang bật và có phiên đăng nhập." });
        draft["content"] = request["content"]!.ToString(); draft["publishAccountId"] = publishId.ToString(); draft["facebookGroupUrl"] = group; draft["status"] = "approved_pending_worker"; draft["approvedAt"] = DateTimeOffset.UtcNow.ToString("O"); doc.Json = draft.ToJsonString();
        db.AddLog(new Audit { Message = $"Đã duyệt bản nháp {id}; chưa đăng, chờ worker và xác minh quyền nhóm." }); await db.SaveChangesAsync(); return ServiceResult.Ok(draft);
    }

    public async Task<ServiceResult> Rewrite(RewriteRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Text) || r.Text.Length > 20000 || r.Instructions.Length > 10000) return ServiceResult.BadRequest(new { error = "Nội dung cần từ 1 đến 20.000 ký tự; chỉ dẫn tối đa 10.000 ký tự." });
        if (!ai.TryAcquire()) return ServiceResult.Conflict(new { error = "AI đang xử lý tác vụ khác. Chỉ một model được chạy mỗi lần." });
        try
        {
            var text = await ai.Rewrite(r);
            var id = Guid.NewGuid(); var result = new { id, model = "bonsai-2:27b", source = r.Text, text, r.Tone, r.Mode, createdAt = DateTimeOffset.UtcNow };
            db.AddDocument(new Document { Id = id, Kind = "rewrites", Json = JsonSerializer.Serialize(result) });
            db.AddLog(new Audit { Message = $"Bonsai hoàn thành rewrite {id}" }); await db.SaveChangesAsync(); return ServiceResult.Ok(result);
        }
        catch (Exception ex) { logger.LogError(ex, "Ollama failed"); db.AddLog(new Audit { Message = "Rewrite thất bại: kiểm tra Ollama hoặc model." }); await db.SaveChangesAsync(); return ServiceResult.Json(new { error = "Ollama không hoàn thành tác vụ. Xem log máy chủ." }, statusCode: 502); }
        finally { ai.Release(); }
    }
}
