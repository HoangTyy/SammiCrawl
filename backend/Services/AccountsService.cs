using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public interface IAccountsService
{
    Task<ServiceResult> Update(Guid id, JsonObject data);
    Task<ServiceResult> Check(Guid id);
    Task<ServiceResult> RevealSession(Guid id);
    Task<ServiceResult> RuntimeError(Guid id, RuntimeAccountError error, HttpContext ctx);
}

public sealed class AccountsService(IProjectRepository db, IDataProtectionProvider protection, FacebookAccountHealth health, IConfiguration config) : IAccountsService
{
    public async Task<ServiceResult> Update(Guid id, JsonObject data)
    {
        var record = await db.FindDocumentAsync(id, "accounts");
        if (record is null) return ServiceResult.NotFound();
        if (string.IsNullOrWhiteSpace(data["name"]?.ToString()) || data["role"]?.ToString() is not ("crawl" or "publish")) return ServiceResult.BadRequest(new { error = "Tài khoản cần tên và vai trò cào/đăng." });
        var original = JsonNode.Parse(record.Json)!;
        var fbStatus = data["facebookStatus"]?.ToString() ?? original["facebookStatus"]?.ToString() ?? "unknown";
        if (!FacebookAccountHealth.Allowed.Contains(fbStatus)) return ServiceResult.BadRequest(new { error = "Trạng thái Facebook không hợp lệ." });
        foreach (var field in new[] { "facebookStatus", "facebookStatusSource", "facebookCheckedAt", "facebookErrorCode", "facebookStatusReason", "lastRuntimeErrorCode", "lastRuntimeErrorReason", "lastRuntimeErrorAt", "lastRuntimeOperation", "lastRuntimeJobId" }) data[field] = original[field]?.DeepClone();
        var cookie = data["cookie"]?.ToString(); data.Remove("cookie");
        data["id"] = id.ToString(); data["createdAt"] = original["createdAt"]?.ToString();
        data["sessionStatus"] = original["sessionStatus"]?.ToString() ?? "Chưa kết nối";
        if (!string.IsNullOrWhiteSpace(cookie)) { record.ProtectedSecret = protection.CreateProtector("FacebookSession.v1").Protect(cookie); data["sessionStatus"] = "Đã lưu phiên · chưa kiểm tra"; }
        record.Json = data.ToJsonString(); db.AddLog(new Audit { Message = $"Đã cập nhật tài khoản Facebook · {id}" }); await db.SaveChangesAsync(); if (!string.IsNullOrWhiteSpace(cookie)) return ServiceResult.Ok(await health.CheckAndSave(id, db)); return ServiceResult.Ok(data);
    }

    public async Task<ServiceResult> RevealSession(Guid id)
    {
        var record = await db.FindDocumentAsync(id, "accounts");
        if (record is null || JsonNode.Parse(record.Json)?["deletedAt"] is not null) return ServiceResult.NotFound();
        if (string.IsNullOrEmpty(record.ProtectedSecret)) return ServiceResult.Ok(new { cookie = "" });
        try { return ServiceResult.Ok(new { cookie = protection.CreateProtector("FacebookSession.v1").Unprotect(record.ProtectedSecret) }); }
        catch { return ServiceResult.Conflict(new { error = "Không giải mã được phiên đã lưu." }); }
    }

    public async Task<ServiceResult> Check(Guid id)
    {
        var result = await health.CheckAndSave(id, db); return result is null ? ServiceResult.NotFound() : ServiceResult.Ok(result);
    }

    public async Task<ServiceResult> RuntimeError(Guid id, RuntimeAccountError error, HttpContext ctx)
    {
        var expected = config["WorkerApiKey"]; var supplied = ctx.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(expected) || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(supplied), System.Text.Encoding.UTF8.GetBytes("Bearer " + expected))) return ServiceResult.Unauthorized();
        var account = await db.FindDocumentAsync(id, "accounts"); var job = await db.FindJobAsync(error.JobId);
        if (account is null || job is null) return ServiceResult.NotFound();
        if (error.Operation is not ("crawl" or "publish")) return ServiceResult.BadRequest(new { error = "Tác vụ phải là crawl hoặc publish." });
        if (error.Code is not ("LOGIN_REQUIRED" or "ACCOUNT_LOCKED" or "CHECKPOINT" or "RATE_LIMITED" or "PERMISSION_DENIED" or "NETWORK_ERROR" or "UNKNOWN_ERROR")) return ServiceResult.BadRequest(new { error = "Mã lỗi không hợp lệ." });
        var jobState = JsonNode.Parse(job.Json)!.AsObject(); var binding = jobState["snapshot"] ?? jobState;
        var bound = error.Operation == "crawl" ? (binding["crawlAccountIds"] as JsonArray)?.Any(a => a?.ToString() == id.ToString()) == true : binding["publishAccountId"]?.ToString() == id.ToString();
        if (!bound) return ServiceResult.BadRequest(new { error = "Tài khoản không được phân công cho tác vụ này." });
        var result = FacebookAccountHealth.Runtime(error.Code); var data = JsonNode.Parse(account.Json)!.AsObject();
        if (error.Code is "LOGIN_REQUIRED" or "ACCOUNT_LOCKED" or "CHECKPOINT" or "RATE_LIMITED") FacebookAccountHealth.Apply(data, result, "runtime_error");
        data["lastRuntimeErrorCode"] = result.Code; data["lastRuntimeErrorReason"] = result.Reason; data["lastRuntimeErrorAt"] = DateTimeOffset.UtcNow.ToString("O"); data["lastRuntimeOperation"] = error.Operation; data["lastRuntimeJobId"] = error.JobId.ToString(); account.Json = data.ToJsonString();
        var state = JsonNode.Parse(job.Json)!.AsObject(); state["status"] = "paused"; state["errorCode"] = result.Code; state["errorReason"] = result.Reason; state["errorAccountId"] = id.ToString(); job.Json = state.ToJsonString();
        db.AddLog(new Audit { Message = $"Lỗi tài khoản {id} · {error.Operation} · tác vụ {error.JobId} · {result.Code}: {result.Reason}" }); await db.SaveChangesAsync(); return ServiceResult.Ok(data);
    }
}
