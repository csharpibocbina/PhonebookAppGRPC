using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Phonebook.Infrastructure.Persistence;

/// <summary>Used only by "dotnet ef migrations add". Migrations target PostgreSQL.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=phonebook;Username=postgres;Password=postgres")
            .Options);
}
