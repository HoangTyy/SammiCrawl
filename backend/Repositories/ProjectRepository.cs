using Microsoft.EntityFrameworkCore;

namespace SammiCrawl.Backend.Repositories;

// One scoped repository shares the EF unit of work across services in each request.
public sealed class ProjectRepository(Store store) : IProjectRepository
{
    public Task<Document?> FindDocumentAsync(Guid id, string kind) => store.Documents.SingleOrDefaultAsync(d => d.Id == id && d.Kind == kind);
    public Task<Document?> FindJobAsync(Guid id) => store.Documents.SingleOrDefaultAsync(d => d.Id == id && (d.Kind == "tasks" || d.Kind == "runs"));
    public Task<List<Document>> ListDocumentsAsync(string kind) => store.Documents.Where(d => d.Kind == kind).OrderByDescending(d => d.CreatedAt).ToListAsync();
    public Task<ProjectUser?> FindUserByEmailAsync(string email) => store.Users.SingleOrDefaultAsync(user => user.Email == email);
    public Task<bool> HasUsersAsync() => store.Users.AnyAsync();
    public Task<List<Audit>> RecentLogsAsync(int count) => store.Logs.OrderByDescending(log => log.Id).Take(count).ToListAsync();
    public string? ProviderName => store.Database.ProviderName;
    public Task<bool> CanConnectAsync() => store.Database.CanConnectAsync();
    public void AddDocument(Document document) => store.Documents.Add(document);
    public void AddUser(ProjectUser user) => store.Users.Add(user);
    public void AddLog(Audit audit) => store.Logs.Add(audit);
    public Task ReloadAsync(Document document) => store.Entry(document).ReloadAsync();
    public Task<int> SaveChangesAsync() => store.SaveChangesAsync();
}
