namespace SammiCrawl.Backend.Repositories;

public interface IProjectRepository
{
    Task<Document?> FindDocumentAsync(Guid id, string kind);
    Task<Document?> FindJobAsync(Guid id);
    Task<List<Document>> ListDocumentsAsync(string kind);
    Task<ProjectUser?> FindUserByEmailAsync(string email);
    Task<bool> HasUsersAsync();
    Task<List<Audit>> RecentLogsAsync(int count);
    string? ProviderName { get; }
    Task<bool> CanConnectAsync();
    void AddDocument(Document document);
    void AddUser(ProjectUser user);
    void AddLog(Audit audit);
    Task ReloadAsync(Document document);
    Task<int> SaveChangesAsync();
}
