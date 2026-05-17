using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.NonFunctionalTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.NonFunctionalTests.Tests;

/// <summary>
/// NF-08: Performance tests.
/// Each API endpoint must respond within acceptable time thresholds.
/// Thresholds are generous for InMemory DB; tighten for production.
/// </summary>
public sealed class PerformanceTests : IClassFixture<NonFunctionalTestFactory>
{
    private readonly HttpClient _client;
    private string? _userSid;
    private string? _managerSid;

    // Thresholds (ms)
    private const int ReadThresholdMs  = 1500;   // GET endpoints
    private const int WriteThresholdMs = 2000;   // POST/PUT endpoints

    public PerformanceTests(NonFunctionalTestFactory factory)
    {
        _client = factory.CreateClient();
        using var db = factory.CreateDbContext();
        TestSeeder.Seed(db);
    }

    private async Task<string> UserSidAsync()
    {
        if (_userSid != null) return _userSid;
        var phone = "+9627" + DateTimeOffset.UtcNow.Ticks.ToString()[^8..];
        var r     = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "PerfUser", phoneNumber = phone, password = "TestPass1!",
            mallID = TestSeeder.MallId
        });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return _userSid = body.GetProperty("sessionId").GetString()!;
    }

    private async Task<string> ManagerSidAsync()
    {
        if (_managerSid != null) return _managerSid;
        var r    = await _client.PostAsJsonAsync("/api/auth/manager-quick-login", new { managerId = TestSeeder.ManagerId });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return _managerSid = body.GetProperty("sessionId").GetString()!;
    }

    private HttpRequestMessage Request(HttpMethod method, string url, string sid)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Session-Id", sid);
        return req;
    }

    private async Task<long> MeasureAsync(Func<Task<HttpResponseMessage>> action)
    {
        var sw = Stopwatch.StartNew();
        await action();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    // ── Auth performance ──────────────────────────────────────────────────

    [Fact]
    public async Task Register_RespondsWithin2000ms()
    {
        var phone = "+9627" + DateTimeOffset.UtcNow.Ticks.ToString()[^8..];
        var elapsed = await MeasureAsync(() =>
            _client.PostAsJsonAsync("/api/auth/register", new
            {
                name = "PerfReg", phoneNumber = phone,
                password = "TestPass1!", mallID = TestSeeder.MallId
            }));

        Assert.True(elapsed < WriteThresholdMs,
            $"Register took {elapsed}ms (threshold: {WriteThresholdMs}ms)");
    }

    [Fact]
    public async Task Login_RespondsWithin1500ms()
    {
        const string phone = "+962799600001";
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "PerfLogin", phoneNumber = phone,
            password = "TestPass1!", mallID = TestSeeder.MallId
        });

        var elapsed = await MeasureAsync(() =>
            _client.PostAsJsonAsync("/api/auth/login", new
            {
                phoneNumber = phone, password = "TestPass1!", mallID = TestSeeder.MallId
            }));

        Assert.True(elapsed < WriteThresholdMs,
            $"Login took {elapsed}ms (threshold: {WriteThresholdMs}ms)");
    }

    // ── Stores performance ─────────────────────────────────────────────────

    [Fact]
    public async Task GetStores_RespondsWithin1500ms()
    {
        var sid     = await UserSidAsync();
        var elapsed = await MeasureAsync(() =>
            _client.SendAsync(Request(HttpMethod.Get, "/api/stores", sid)));

        Assert.True(elapsed < ReadThresholdMs,
            $"GET /api/stores took {elapsed}ms (threshold: {ReadThresholdMs}ms)");
    }

    // ── Announcements performance ──────────────────────────────────────────

    [Fact]
    public async Task GetAnnouncements_RespondsWithin1500ms()
    {
        var sid     = await UserSidAsync();
        var elapsed = await MeasureAsync(() =>
            _client.SendAsync(Request(HttpMethod.Get, "/api/announcements", sid)));

        Assert.True(elapsed < ReadThresholdMs,
            $"GET /api/announcements took {elapsed}ms (threshold: {ReadThresholdMs}ms)");
    }

    // ── Offers performance ─────────────────────────────────────────────────

    [Fact]
    public async Task GetOffers_RespondsWithin1500ms()
    {
        var sid     = await UserSidAsync();
        var elapsed = await MeasureAsync(() =>
            _client.SendAsync(Request(HttpMethod.Get, "/api/offers", sid)));

        Assert.True(elapsed < ReadThresholdMs,
            $"GET /api/offers took {elapsed}ms (threshold: {ReadThresholdMs}ms)");
    }

    // ── Dashboard performance ──────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardSummary_RespondsWithin2000ms()
    {
        var sid     = await ManagerSidAsync();
        var elapsed = await MeasureAsync(() =>
            _client.SendAsync(Request(HttpMethod.Get, "/api/dashboard/summary", sid)));

        Assert.True(elapsed < WriteThresholdMs,
            $"GET /api/dashboard/summary took {elapsed}ms (threshold: {WriteThresholdMs}ms)");
    }

    [Fact]
    public async Task GetDashboardSales_RespondsWithin2000ms()
    {
        var sid     = await ManagerSidAsync();
        var elapsed = await MeasureAsync(() =>
            _client.SendAsync(Request(HttpMethod.Get, "/api/dashboard/sales", sid)));

        Assert.True(elapsed < WriteThresholdMs,
            $"GET /api/dashboard/sales took {elapsed}ms (threshold: {WriteThresholdMs}ms)");
    }

    // ── Coupons performance ────────────────────────────────────────────────

    [Fact]
    public async Task GetCoupons_RespondsWithin1500ms()
    {
        var sid     = await UserSidAsync();
        var elapsed = await MeasureAsync(() =>
            _client.SendAsync(Request(HttpMethod.Get, "/api/coupons", sid)));

        Assert.True(elapsed < ReadThresholdMs,
            $"GET /api/coupons took {elapsed}ms (threshold: {ReadThresholdMs}ms)");
    }
}
