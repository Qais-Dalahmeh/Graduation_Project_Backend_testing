using Graduation_Project_Backend.Data;
using Graduation_Project_Backend.Service.Realtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;
using Testcontainers.PostgreSql;
using Xunit;

namespace Graduation_Project_Backend.IntegrationTests.Infrastructure;

/// <summary>
/// Shared integration test factory.
/// Spins up a REAL PostgreSQL container via Docker (Testcontainers),
/// runs EF Core migrations, and seeds base data once per collection.
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // ── Real PostgreSQL container via Docker ─────────────────────────────
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("integration_test_db")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .Build();

    // ── Start the container and create schema before any test runs ──────────
    public async Task InitializeAsync()
    {
        // 1. Start the real PostgreSQL Docker container
        await _postgres.StartAsync();

        // 2. Trigger app build (calls ConfigureWebHost which sets the connection string)
        //    then create the full schema directly from the EF Core model.
        //    EnsureCreatedAsync is more reliable than MigrateAsync in test environments
        //    because it creates all tables directly without depending on migration files.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Point backend to the real PostgreSQL container
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                // Disable Program.cs migrations — we run schema creation ourselves
                ["RunMigrations"]                       = "false",
                // Stub chatbot config so app doesn't crash
                ["ChatbotSettings:ApiKey"]              = "test-key",
                ["ChatbotSettings:Endpoint"]            = "http://localhost:9999/fake"
            });
        });

        builder.ConfigureServices(services =>
        {
            // ── Remove existing PostgreSQL DbContext registration ─────────
            var dbOpts = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbOpts != null) services.Remove(dbOpts);

            // ── Register real PostgreSQL using container connection string ─
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            // ── Replace realtime SignalR service with no-op ───────────────
            var rtDesc = services.SingleOrDefault(
                d => d.ServiceType == typeof(IUserPointsUpdatesService));
            if (rtDesc != null) services.Remove(rtDesc);
            services.AddSingleton<IUserPointsUpdatesService, NoOpUserPointsUpdatesService>();
        });
    }

    /// <summary>Opens a scoped AppDbContext for seeding or direct DB assertions.</summary>
    public AppDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    // ── Stop and remove the container after all tests finish ─────────────
    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}

// ── No-op realtime service ────────────────────────────────────────────────
file sealed class NoOpUserPointsUpdatesService : IUserPointsUpdatesService
{
    public ChannelReader<DTOs.Realtime.UserPointsChangedDto> Subscribe(
        Guid userId, CancellationToken cancellationToken = default)
        => Channel.CreateUnbounded<DTOs.Realtime.UserPointsChangedDto>().Reader;

    public ValueTask PublishAsync(Guid userId, int totalPoints, string source,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
