namespace SammiCrawl.Backend.Models;

public class Document { public Guid Id { get; set; } public string Kind { get; set; } = ""; public string Json { get; set; } = "{}"; public string? ProtectedSecret { get; set; } public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
