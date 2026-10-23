namespace SammiCrawl.Backend.Models;

public class ProjectUser { public Guid Id { get; set; } = Guid.NewGuid(); public string Email { get; set; } = ""; public string PasswordHash { get; set; } = ""; }
