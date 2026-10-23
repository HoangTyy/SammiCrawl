namespace SammiCrawl.Backend.Models;

public class Audit { public long Id { get; set; } public string Message { get; set; } = ""; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
