using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.NonFunctionalTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.NonFunctionalTests.Tests;

/// <summary>
/// NF-09: Concurrency tests.
/// Verifies the system handles multiple simultaneous requests without failures.
/// </summary>
public sealed class ConcurrencyTests : IClassFixture<NonFunctionalTestFactory>
{
    private readonly NonFunctionalTestFactory _factory;
    private string? _userSid;
    private string? _managerSid;

    public ConcurrencyTests(NonFunctionalTestFactory factory)
    {
        _factory = factory;
        using var db = factory.CreateDbContext();
        TestSeeder.Seed(db);
    }

    private async Task<string> UserSidAsync()
    {
        if (_userSid != null) return _userSid;
        var client = _factory.CreateClient();
        var phone  = "+9627" + DateTimeOffset.UtcNow.Ticks.ToString()[^8..];
        var r      = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "ConcUser", phoneNumber = phone, password = "TestPass1!",
            mallID = TestSeeder.MallId
        });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return _userSid = body.GetProperty("sessionId").GetString()!;
    }

    private async Task<string> ManagerSidAsync()
    {
        if (_managerSid != null) return _managerSid;
        var client = _factory.CreateClient();
        var r      = await client.PostAsJsonAsync("/api/auth/manager-quick-login",
            new { managerId = TestSeeder.ManagerId });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return _managerSid = body.GetProperty("sessionId").GetString()!;
    }

    // ── Concurrent GET /api/stores ────────────────────────────────────────

    [Fact]
    public async Task GetStores_10ConcurrentRequests_AllReturn200()
    {
        var sid = await UserSidAsync();

        var tasks = Enumerable.Range(0, 10).Select(_ =>
        {
            var client = _factory.CreateClient();
            var req    = new HttpRequestMessage(HttpMethod.Get, "/api/stores");
            req.Headers.Add("X-Session-Id", sid);
            return client.SendAsync(req);
        });

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r =>
            Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }

    // ── Concurrent GET /api/announcements ────────────────────────────────

    [Fact]
    public async Task GetAnnouncements_10ConcurrentRequests_AllReturn200()
    {
        var sid = await UserSidAsync();

        var tasks = Enumerable.Range(0, 10).Select(_ =>
        {
            var client = _factory.CreateClient();
            var req    = new HttpRequestMessage(HttpMethod.Get, "/api/announcements");
            req.Headers.Add("X-Session-Id", sid);
            return client.SendAsync(req);
        });

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r =>
            Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }

    // ── Concurrent User Registrations ────────────────────────────────────

    [Fact]
    public async Task Register_5UsersSimultaneously_AllSucceed()
    {
        var tasks = Enumerable.Range(0, 5).Select(i =>
        {
            var client = _factory.CreateClient();
            var phone  = "+96270000" + i.ToString("D4");
            return client.PostAsJsonAsync("/api/auth/register", new
            {
                name        = $"ConcUser{i}",
                phoneNumber = phone,
                password    = "TestPass1!",
                mallID      = TestSeeder.MallId
            });
        });

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r =>
            Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }

    // ── Concurrent Dashboard Summary ──────────────────────────────────────

    [Fact]
    public async Task GetDashboardSummary_5ConcurrentRequests_AllReturn200()
    {
        var sid = await ManagerSidAsync();

        var tasks = Enumerable.Range(0, 5).Select(_ =>
        {
            var client = _factory.CreateClient();
            var req    = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/summary");
            req.Headers.Add("X-Session-Id", sid);
            return client.SendAsync(req);
        });

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r =>
            Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }

    // ── Concurrent Coupon Listing ─────────────────────────────────────────

    [Fact]
    public async Task GetCoupons_10ConcurrentRequests_AllReturn200()
    {
        var sid = await UserSidAsync();

        var tasks = Enumerable.Range(0, 10).Select(_ =>
        {
            var client = _factory.CreateClient();
            var req    = new HttpRequestMessage(HttpMethod.Get, "/api/coupons");
            req.Headers.Add("X-Session-Id", sid);
            return client.SendAsync(req);
        });

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r =>
            Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }
}
