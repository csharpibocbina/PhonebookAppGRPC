using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Phonebook.Application.Auth;
using Phonebook.Domain.Entities;

namespace Phonebook.Infrastructure.Persistence;

public sealed class DatabaseInitializer(AppDbContext db, IPasswordHasher passwordHasher, TimeProvider time, ILogger<DatabaseInitializer> logger)
{
    public const string DemoEmail = "demo@phonebook.local";
    public const string DemoPassword = "Demo123!";

    public async Task InitializeAsync(bool seedDemoData, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsRelational() && db.Database.GetMigrations().Any() && !db.Database.IsSqlite())
            await db.Database.MigrateAsync(cancellationToken);   // PostgreSQL: apply migrations
        else
            await db.Database.EnsureCreatedAsync(cancellationToken); // SQLite: create schema from the model

        if (seedDemoData && !await db.Users.AnyAsync(cancellationToken))
            await SeedAsync(cancellationToken);
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var demo = new User
        {
            Email = DemoEmail,
            DisplayName = "Demo User",
            PasswordHash = passwordHasher.Hash(DemoPassword),
            CreatedAt = now,
        };

        (string First, string Last, string Phone, string? Email, bool Fav)[] people =
        [
            ("Maria", "Santos", "+63 917 555 0101", "maria.santos@example.com", true),
            ("John", "Reyes", "+63 918 555 0102", "john.reyes@example.com", false),
            ("Ana", "Cruz", "+63 919 555 0103", null, true),
            ("Carlos", "Garcia", "+1 415 555 0104", "carlos@example.com", false),
            ("Liza", "Mendoza", "+63 920 555 0105", "liza.m@example.com", false),
            ("Paolo", "Villanueva", "+63 921 555 0106", null, false),
            ("Grace", "Tan", "+65 8555 0107", "grace.tan@example.com", true),
            ("Miguel", "Ramos", "+63 922 555 0108", "miguel.ramos@example.com", false),
            ("Sofia", "Lim", "+63 923 555 0109", null, false),
            ("David", "Aquino", "+44 20 5555 0110", "d.aquino@example.com", false),
            ("Bea", "Castillo", "+63 924 555 0111", "bea.castillo@example.com", true),
            ("Rafael", "Torres", "+63 925 555 0112", null, false),
        ];

        foreach (var (first, last, phone, email, fav) in people)
        {
            demo.Contacts.Add(new Contact
            {
                FirstName = first, LastName = last, PhoneNumber = phone, Email = email, IsFavorite = fav,
                CreatedAt = now, UpdatedAt = now,
            });
        }

        db.Users.Add(demo);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded demo user {Email} with {Count} contacts", DemoEmail, people.Length);
    }
}
