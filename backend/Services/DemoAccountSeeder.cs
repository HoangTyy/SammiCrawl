using Microsoft.AspNetCore.Identity;

namespace SammiCrawl.Backend.Services;

public static class DemoAccountSeeder
{
    // Explicit CLI operation only; never run automatically when the website starts.
    public static async Task Seed(IProjectRepository repository, IConfiguration configuration)
    {
        var email = configuration["DemoAccount:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["DemoAccount:Password"];
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || string.IsNullOrEmpty(password) || password.Length < 12)
            throw new InvalidOperationException("Configure DemoAccount email and password (at least 12 characters) first.");
        if ((await repository.FindUserByEmailAsync(email)) is not null)
        {
            Console.WriteLine("Demo account already exists; password was not changed.");
            return;
        }
        var user = new ProjectUser { Email = email };
        user.PasswordHash = new PasswordHasher<ProjectUser>().HashPassword(user, password);
        repository.AddUser(user);
        await repository.SaveChangesAsync();
        Console.WriteLine("Demo account created. Remove or change it before public deployment.");
    }
}
