using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using Testcontainers.MsSql;

namespace PawConnect.IntegrationTests.Infrastructure;

public sealed class SqlServerTestcontainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")
        .WithPassword("PawConnect1!SqlTests")
        .Build();

    public async Task InitializeAsync()
    {
        await container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await container.DisposeAsync();
    }

    public async Task<ApplicationDbContext> CreateMigratedContextAsync(
        string? databaseName = null,
        CancellationToken cancellationToken = default)
    {
        var context = new ApplicationDbContext(CreateOptions(databaseName ?? CreateDatabaseName()));
        await context.Database.MigrateAsync(cancellationToken);
        return context;
    }

    public IDbContextFactory<ApplicationDbContext> CreateContextFactory(string databaseName)
    {
        return new SqlServerApplicationDbContextFactory(CreateOptions(databaseName));
    }

    public static string CreateDatabaseName()
    {
        return $"PawConnectIntegration_{Guid.NewGuid():N}";
    }

    private DbContextOptions<ApplicationDbContext> CreateOptions(string databaseName)
    {
        var databaseConnectionString = ReplaceInitialCatalog(container.GetConnectionString(), databaseName);

        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(databaseConnectionString)
            .Options;
    }

    private static string ReplaceInitialCatalog(string connectionString, string databaseName)
    {
        var parts = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part =>
                !part.StartsWith("Database=", StringComparison.OrdinalIgnoreCase) &&
                !part.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
            .Append($"Database={databaseName}");

        return string.Join(';', parts);
    }

    private sealed class SqlServerApplicationDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext()
        {
            return new ApplicationDbContext(options);
        }
    }
}
