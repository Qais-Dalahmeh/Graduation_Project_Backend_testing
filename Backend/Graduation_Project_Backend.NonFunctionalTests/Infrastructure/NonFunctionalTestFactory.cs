using Graduation_Project_Backend.Data;
using Graduation_Project_Backend.Service.Realtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;

namespace Graduation_Project_Backend.NonFunctionalTests.Infrastructure;

/// <summary>
/// In-process test server using WebApplicationFactory.
/// Replaces PostgreSQL with InMemory DB and disables migrations.
/// Each instance gets its own isolated database.
/// </summary>
public sealed class NonFunctionalTestFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "NFTest_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Provide dummy config so app doesn't crash on missing env vars
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RunMigrations"]                         = "false",
                ["ConnectionStrings:DefaultConnection"]   = "Host=localhost;Database=ignored;Username=ignored;Password=ignored",
                ["ChatbotSettings:ApiKey"]                = "test-key",
                ["ChatbotSettings:Endpoint"]              = "http://localhost:9999/fake"
            });
        });

        builder.ConfigureServices(services =>
        {
            // ── Remove real PostgreSQL DbContext ──────────────────────────
            var dbOpts = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbOpts != null) services.Remove(dbOpts);

            // ── Add InMemory DbContext ────────────────────────────────────
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // ── Replace realtime hub with no-op ───────────────────────────
            var rtDesc = services.SingleOrDefault(
                d => d.ServiceType == typeof(IUserPointsUpdatesService));
            if (rtDesc != null) services.Remove(rtDesc);
            services.AddSingleton<IUserPointsUpdatesService, NoOpUserPointsUpdatesService>();
        });
    }

    // ── Seed and expose a scoped DB for test helpers ─────────────────────
    public AppDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
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
