using System.Net;
using System.Net.Http.Json;
using Graduation_Project_Backend.NonFunctionalTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.NonFunctionalTests.Tests;

/// <summary>
/// NF-02: Security tests — every protected endpoint must return 401 when
/// the request carries no session header.
/// </summary>
public sealed class SecurityTests : IClassFixture<NonFunctionalTestFactory>
{
    private readonly HttpClient _client;

    private static readonly (HttpMethod Method, string Url)[] ProtectedEndpoints =
    [
        (HttpMethod.Get,    "/api/stores"),
        (HttpMethod.Get,    "/api/announcements"),
        (HttpMethod.Get,    "/api/offers"),
        (HttpMethod.Get,    "/api/coupons"),
        (HttpMethod.Get,    "/api/stores/manage"),
        (HttpMethod.Get,    "/api/announcements/manage"),
        (HttpMethod.Get,    "/api/offers/manage"),
        (HttpMethod.Get,    "/api/dashboard/summary"),
        (HttpMethod.Get,    "/api/dashboard/sales"),
        (HttpMethod.Get,    "/api/dashboard/points"),
        (HttpMethod.Get,    "/api/dashboard/coupons"),
        (HttpMethod.Get,    "/api/dashboard/activity"),
        (HttpMethod.Get,    "/api/coupons/user"),
    ];

    public SecurityTests(NonFunctionalTestFactory factory)
    {
        _client = factory.CreateClient();
        using var db = factory.CreateDbContext();
        TestSeeder.Seed(db);
    }

    [Theory]
    [MemberData(nameof(GetProtectedEndpoints))]
    public async Task ProtectedEndpoint_WithoutSession_Returns401(HttpMethod method, string url)
    {
        var request  = new HttpRequestMessage(method, url);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static IEnumerable<object[]> GetProtectedEndpoints()
        => ProtectedEndpoints.Select(e => new object[] { e.Method, e.Url });

    // ── Manager-only endpoints must return 403 for regular users ─────────

    [Fact]
    public async Task CreateStore_AsRegularUser_Returns403()
    {
        // Register a normal user
        const string phone = "+962799500001";
        var reg = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Regular User", phoneNumber = phone, password = "TestPass1!",
            mallID = TestSeeder.MallId
        });
        var body = await reg.Content.ReadAsStringAsync();
        var json  = System.Text.Json.JsonDocument.Parse(body).RootElement;
        var sid   = json.GetProperty("sessionId").GetString()!;

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/stores");
        req.Headers.Add("X-Session-Id", sid);
        req.Content = System.Net.Http.Json.JsonContent.Create(new
        {
            name = "Hacked Store", mallID = TestSeeder.MallId
        });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboardSummary_AsRegularUser_Returns403()
    {
        const string phone = "+962799500002";
        var reg = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Regular User2", phoneNumber = phone, password = "TestPass1!",
            mallID = TestSeeder.MallId
        });
        var json = System.Text.Json.JsonDocument.Parse(
            await reg.Content.ReadAsStringAsync()).RootElement;
        var sid = json.GetProperty("sessionId").GetString()!;

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/summary");
        req.Headers.Add("X-Session-Id", sid);

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
