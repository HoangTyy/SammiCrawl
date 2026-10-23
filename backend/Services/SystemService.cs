using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public interface ISystemService
{
    Task<ServiceResult> Health();
    Task<ServiceResult> Logs();
}

public sealed class SystemService(IProjectRepository db, AiRunner ai) : ISystemService
{
    public async Task<ServiceResult> Health() { return ServiceResult.Ok(new { database = await db.CanConnectAsync(), databaseProvider = db.ProviderName, ai = await ai.Status(), crawlWorker = "Chưa triển khai", publishWorker = "Chưa triển khai" }); }

    public async Task<ServiceResult> Logs() { return ServiceResult.Ok((await db.RecentLogsAsync(100)).Select(l => $"[{l.CreatedAt:O}] {l.Message}")); }
}
