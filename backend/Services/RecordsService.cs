using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public interface IRecordsService
{
    Task<ServiceResult> List(string kind, bool? trash);
    Task<ServiceResult> Create(string kind, JsonObject data);
    Task<ServiceResult> Update(string kind, Guid id, JsonObject data);
    Task<ServiceResult> Delete(string kind, Guid id);
    Task<ServiceResult> Restore(string kind, Guid id);
    Task<ServiceResult> CancelTask(Guid id);
}

public sealed class RecordsService(IProjectRepository db, IDataProtectionProvider protection, FacebookAccountHealth health) : IRecordsService
{
    public async Task<ServiceResult> List(string kind, bool? trash)
    {
        if (!RecordKinds.All.Contains(kind)) return ServiceResult.NotFound();
        var docs = await db.ListDocumentsAsync(kind);
        return ServiceResult.Ok(docs.Select(x => JsonNode.Parse(x.Json)).Where(x => (x?["deletedAt"] is not null) == (trash == true)));
    }

    public async Task<ServiceResult> Create(string kind, JsonObject data)
    {
        if (!RecordKinds.All.Contains(kind)) return ServiceResult.NotFound();
        if (kind is "runs" or "articles" or "drafts") return ServiceResult.StatusCode(405);
        if (kind == "accounts" && (data["role"]?.ToString() is not ("crawl" or "publish") || string.IsNullOrWhiteSpace(data["name"]?.ToString()))) return ServiceResult.BadRequest(new { error = "Tài khoản cần tên và vai trò cào/đăng." });
        if (kind == "accounts")
        {
            var status = data["facebookStatus"]?.ToString() ?? "unknown";
            if (!FacebookAccountHealth.Allowed.Contains(status)) return ServiceResult.BadRequest(new { error = "Trạng thái Facebook không hợp lệ." });
            data["facebookStatus"] = "unknown"; data["facebookStatusSource"] = "not_checked";
        }
        if (kind == "brands" && string.IsNullOrWhiteSpace(data["name"]?.ToString())) return ServiceResult.BadRequest(new { error = "Thiếu tên thương hiệu." });
        if (kind == "brands" && BrandPolicy.Validate(data) is string policyError) return ServiceResult.BadRequest(new { error = policyError });
        if (kind == "brands" && await BrandPolicy.ValidatePublishers(data, db) is string publisherError) return ServiceResult.BadRequest(new { error = publisherError });
        if (kind is "tasks" or "presets")
        {
            foreach (var (field, min, max) in new (string, double, double)[] {
            ("maxPosts",1,1000),("maxScrolls",1,2000),("delayMinMs",500,120000),("delayMaxMs",500,120000),
            ("minLikes",0,10000000),("minComments",0,10000000),("minAiScore",0,100),("topPosts",1,1000),("rewriteTemperature",0,1.5),("rewriteMinWords",10,3000),("rewriteMaxWords",10,3000),("accountPostLimit",1,1000),("accountRestMinMs",1000,3600000),("accountRestMaxMs",1000,3600000) })
            {
                if (data[field] is not null && (data[field] is not JsonValue v || !v.TryGetValue<double>(out var number) || !double.IsFinite(number) || number < min || number > max || (field != "rewriteTemperature" && number != Math.Truncate(number))))
                    return ServiceResult.BadRequest(new { error = $"Thông số {field} phải nằm trong khoảng {min}–{max}." });
            }
            if (data["delayMinMs"] is not null && data["delayMaxMs"] is not null && data["delayMinMs"]!.GetValue<double>() > data["delayMaxMs"]!.GetValue<double>())
                return ServiceResult.BadRequest(new { error = "Độ trễ tối thiểu không được lớn hơn tối đa." });
            if (data["branch"] is not null && data["branch"]!.ToString() is not ("repost" or "rewrite")) return ServiceResult.BadRequest(new { error = "Nhánh xử lý phải là repost hoặc rewrite." });
            if (data["crawlAccountIds"] is not null)
            {
                if (data["crawlAccountIds"] is not JsonArray ids) return ServiceResult.BadRequest(new { error = "Danh sách tài khoản cào không hợp lệ." });
                foreach (var accountId in ids)
                {
                    if (!Guid.TryParse(accountId?.ToString(), out var parsedId)) return ServiceResult.BadRequest(new { error = "ID tài khoản cào không hợp lệ." });
                    var account = await db.FindDocumentAsync(parsedId, "accounts");
                    var a = account is null ? null : JsonNode.Parse(account.Json);
                    if (a?["role"]?.ToString() != "crawl" || a?["enabled"]?.GetValue<bool>() == false) return ServiceResult.BadRequest(new { error = "Chỉ chọn tài khoản cào đang được bật." });
                }
            }
            var publishId = data["publishAccountId"]?.ToString();
            if (!string.IsNullOrEmpty(publishId))
            {
                if (!Guid.TryParse(publishId, out var parsedId)) return ServiceResult.BadRequest(new { error = "ID tài khoản đăng không hợp lệ." });
                var account = await db.FindDocumentAsync(parsedId, "accounts"); var a = account is null ? null : JsonNode.Parse(account.Json);
                if (a?["role"]?.ToString() != "publish" || a?["enabled"]?.GetValue<bool>() == false) return ServiceResult.BadRequest(new { error = "Chỉ chọn tài khoản đăng đang được bật." });
            }
            foreach (var pair in new[] { ("rewriteMinWords", "rewriteMaxWords"), ("accountRestMinMs", "accountRestMaxMs") })
                if (data[pair.Item1] is not null && data[pair.Item2] is not null && data[pair.Item1]!.GetValue<double>() > data[pair.Item2]!.GetValue<double>()) return ServiceResult.BadRequest(new { error = "Giá trị tối thiểu không được lớn hơn tối đa." });
            data["targetType"] = "page";
            data["extractMedia"] = true;
            data.Remove("scrollDelay");
        }
        if (kind == "tasks")
        {
            if (!Uri.TryCreate(data["url"]?.ToString(), UriKind.Absolute, out var url) || url.Scheme != "https" || !(url.Host == "facebook.com" || url.Host.EndsWith(".facebook.com"))) return ServiceResult.BadRequest(new { error = "Cần URL https của Facebook." });
            var max = data["maxPosts"]?.GetValue<int>() ?? 0;
            if (max < 1 || max > 1000) return ServiceResult.BadRequest(new { error = "Số bài phải từ 1 đến 1000." });
            data["status"] = "paused"; data["progress"] = 0; data["crawledPosts"] = 0;
            data["workerNode"] = "Chờ cài worker"; data["ipAddress"] = "—"; data["latencyMs"] = 0;
        }
        var id = Guid.NewGuid(); data["id"] = id.ToString(); data["createdAt"] = DateTimeOffset.UtcNow.ToString("O");
        if (kind == "accounts") { var sessionError = FacebookSessionInput.Normalize(data); if (sessionError is not null) return ServiceResult.BadRequest(new { error = sessionError }); }
        string? secret = data["cookie"]?.ToString(); data.Remove("cookie");
        if (kind == "accounts") data["sessionStatus"] = string.IsNullOrEmpty(secret) ? "Chưa kết nối" : "Đã lưu phiên · chưa kiểm tra";
        db.AddDocument(new Document { Id = id, Kind = kind, Json = data.ToJsonString(), ProtectedSecret = string.IsNullOrEmpty(secret) ? null : protection.CreateProtector("FacebookSession.v1").Protect(secret) });
        db.AddLog(new Audit { Message = $"Đã lưu {kind} · {id}" }); await db.SaveChangesAsync(); if (kind == "accounts") return ServiceResult.Ok(await health.CheckAndSave(id, db)); return ServiceResult.Ok(data);
    }

    public async Task<ServiceResult> Update(string kind, Guid id, JsonObject data)
    {
        if (kind is not ("brands" or "presets" or "rewrites" or "accounts")) return ServiceResult.NotFound();
        var record = await db.FindDocumentAsync(id, kind);
        if (record is null) return ServiceResult.NotFound();
        var original = JsonNode.Parse(record.Json)!.AsObject();
        if (original["deletedAt"] is not null) return ServiceResult.Conflict(new { error = "Khôi phục bản ghi trước khi chỉnh sửa." });
        if (kind == "brands" && data.ContainsKey("updatedAt") && data["updatedAt"]?.ToString() != original["updatedAt"]?.ToString()) return ServiceResult.Conflict(new { error = "Hồ sơ vừa đổi ở nơi khác. Tải lại trước khi sửa." });
        var fields = kind switch
        {
            "brands" => new[] { "facebookGroupUrl", "name", "description", "language", "tone", "writingStyle", "audience", "callToAction", "requiredKeywords", "forbiddenKeywords", "rules", "allowedTopics", "forbiddenContent", "verifyInformation", "requireMediaRights", "requireSponsorLabel", "channels" },
            "presets" => new[] { "url", "destinationPageUrl", "rewriteMinWords", "rewriteMaxWords", "accountPostLimit", "accountRestMinMs", "accountRestMaxMs", "name", "targetType", "maxPosts", "maxScrolls", "delayMinMs", "delayMaxMs", "minLikes", "minComments", "keywords", "topicDescription", "includeComments", "crawlAccountIds", "publishAccountId", "brandId", "branch", "minAiScore", "topPosts", "rewriteInstructions", "rewriteTemperature", "requireApproval" },
            _ => new[] { "text" }
        };
        foreach (var field in fields) if (data.ContainsKey(field)) original[field] = data[field]?.DeepClone();
        if (kind is "brands" or "presets" && string.IsNullOrWhiteSpace(original["name"]?.ToString())) return ServiceResult.BadRequest(new { error = "Tên không được để trống." });
        if (kind == "rewrites" && string.IsNullOrWhiteSpace(original["text"]?.ToString())) return ServiceResult.BadRequest(new { error = "Nội dung không được để trống." });
        if (kind == "brands" && BrandPolicy.Validate(original) is string policyError) return ServiceResult.BadRequest(new { error = policyError });
        if (kind == "brands" && await BrandPolicy.ValidatePublishers(original, db) is string publisherError) return ServiceResult.BadRequest(new { error = publisherError });
        if (kind == "presets")
        {
            foreach (var (field, min, max) in new (string, double, double)[] { ("maxPosts", 1, 1000), ("maxScrolls", 1, 2000), ("delayMinMs", 500, 120000), ("delayMaxMs", 500, 120000), ("minLikes", 0, 10000000), ("minComments", 0, 10000000), ("minAiScore", 0, 100), ("topPosts", 1, 1000), ("rewriteTemperature", 0, 1.5), ("rewriteMinWords", 10, 3000), ("rewriteMaxWords", 10, 3000), ("accountPostLimit", 1, 1000), ("accountRestMinMs", 1000, 3600000), ("accountRestMaxMs", 1000, 3600000) })
                if (original[field] is not null && (original[field] is not JsonValue v || !v.TryGetValue<double>(out var n) || !double.IsFinite(n) || n < min || n > max || (field != "rewriteTemperature" && n != Math.Truncate(n)))) return ServiceResult.BadRequest(new { error = $"Thông số {field} không hợp lệ." });
            if (original["delayMinMs"] is not null && original["delayMaxMs"] is not null && original["delayMinMs"]!.GetValue<double>() > original["delayMaxMs"]!.GetValue<double>()) return ServiceResult.BadRequest(new { error = "Độ trễ tối thiểu không được lớn hơn tối đa." });
            if (original["branch"] is not null && original["branch"]!.ToString() is not ("repost" or "rewrite")) return ServiceResult.BadRequest(new { error = "Nhánh xử lý không hợp lệ." });
            foreach (var pair in new[] { ("rewriteMinWords", "rewriteMaxWords"), ("accountRestMinMs", "accountRestMaxMs") })
                if (original[pair.Item1] is not null && original[pair.Item2] is not null && original[pair.Item1]!.GetValue<double>() > original[pair.Item2]!.GetValue<double>()) return ServiceResult.BadRequest(new { error = "Giá trị tối thiểu không được lớn hơn tối đa." });
            original["targetType"] = "page"; original["extractMedia"] = true;
        }
        original["updatedAt"] = DateTimeOffset.UtcNow.ToString("O");
        if (kind == "brands") return await db.TryUpdateBrandAsync(id, record.Json, original.ToJsonString(), $"Đã chỉnh sửa brands · {id}")
            ? ServiceResult.Ok(original) : ServiceResult.Conflict(new { error = "Hồ sơ vừa được cập nhật ở nơi khác. Tải lại trước khi sửa." });
        record.Json = original.ToJsonString();
        db.AddLog(new Audit { Message = $"Đã chỉnh sửa {kind} · {id}" }); await db.SaveChangesAsync(); return ServiceResult.Ok(original);
    }

    public async Task<ServiceResult> Delete(string kind, Guid id)
    {
        if (kind is not ("brands" or "presets" or "rewrites" or "accounts")) return ServiceResult.NotFound();
        var record = await db.FindDocumentAsync(id, kind); if (record is null) return ServiceResult.NotFound();
        var data = JsonNode.Parse(record.Json)!; data["deletedAt"] = DateTimeOffset.UtcNow.ToString("O"); if (kind == "accounts") data["enabled"] = false; record.Json = data.ToJsonString();
        db.AddLog(new Audit { Message = $"Đã chuyển {kind} vào thùng rác · {id}" }); await db.SaveChangesAsync(); return ServiceResult.Ok();
    }

    public async Task<ServiceResult> Restore(string kind, Guid id)
    {
        if (kind is not ("brands" or "presets" or "rewrites" or "accounts")) return ServiceResult.NotFound();
        var record = await db.FindDocumentAsync(id, kind); if (record is null) return ServiceResult.NotFound();
        var data = JsonNode.Parse(record.Json)!.AsObject(); data.Remove("deletedAt"); record.Json = data.ToJsonString();
        db.AddLog(new Audit { Message = $"Đã khôi phục {kind} · {id}" }); await db.SaveChangesAsync(); return ServiceResult.Ok(data);
    }

    public async Task<ServiceResult> CancelTask(Guid id) { var d = await db.FindDocumentAsync(id, "tasks"); if (d == null) return ServiceResult.NotFound(); var j = JsonNode.Parse(d.Json)!; j["status"] = "paused"; j["workerNode"] = "Đã hủy"; d.Json = j.ToJsonString(); db.AddLog(new Audit { Message = $"Đã hủy tác vụ {id}" }); await db.SaveChangesAsync(); return ServiceResult.Ok(); }
}
