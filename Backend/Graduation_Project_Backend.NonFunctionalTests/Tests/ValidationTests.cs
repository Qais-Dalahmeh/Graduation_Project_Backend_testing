using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.NonFunctionalTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.NonFunctionalTests.Tests;

/// <summary>
/// NF-10: Input validation tests.
/// Every invalid input must return 400 Bad Request with a structured error body.
/// </summary>
public sealed class ValidationTests : IClassFixture<NonFunctionalTestFactory>
{
    private readonly HttpClient _client;
    private string? _managerSid;

    public ValidationTests(NonFunctionalTestFactory factory)
    {
        _client = factory.CreateClient();
        using var db = factory.CreateDbContext();
        TestSeeder.Seed(db);
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

    // ── Auth validation ───────────────────────────────────────────────────

    [Fact]
    public async Task Register_InvalidPhoneFormat_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name        = "Bad Phone",
            phoneNumber = "not-a-phone",        // invalid
            password    = "TestPass1!",
            mallID      = TestSeeder.MallId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_EmptyName_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name        = "",                    // empty name
            phoneNumber = "+962799700001",
            password    = "TestPass1!",
            mallID      = TestSeeder.MallId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_EmptyPassword_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name        = "EmptyPassUser",
            phoneNumber = "+962799700002",
            password    = "",                  // empty password → validation error
            mallID      = TestSeeder.MallId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Offer validation ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateOffer_NullTitle_Returns400()
    {
        var sid = await ManagerSidAsync();
        var req = Request(HttpMethod.Post, "/api/offers", sid);
        req.Content = JsonContent.Create(new
        {
            storeId = TestSeeder.StoreId,
            title   = (string?)null,
            startAt = DateTimeOffset.UtcNow,
            endAt   = DateTimeOffset.UtcNow.AddDays(5)
        });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOffer_StartAfterEnd_Returns400()
    {
        var sid = await ManagerSidAsync();
        var req = Request(HttpMethod.Post, "/api/offers", sid);
        req.Content = JsonContent.Create(new
        {
            storeId = TestSeeder.StoreId,
            title   = "Bad Date Offer",
            startAt = DateTimeOffset.UtcNow.AddDays(10),  // start > end
            endAt   = DateTimeOffset.UtcNow.AddDays(1)
        });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Announcement validation ───────────────────────────────────────────

    [Fact]
    public async Task CreateAnnouncement_EmptyTitle_Returns400()
    {
        var sid = await ManagerSidAsync();
        var req = Request(HttpMethod.Post, "/api/announcements", sid);
        req.Content = JsonContent.Create(new
        {
            title            = "",           // blank
            content          = "Some content",
            announcementType = "general",
            priority         = "normal",
            isActive         = true,
            isPinned         = false,
            startDate        = DateTimeOffset.UtcNow.AddDays(-1),
            endDate          = DateTimeOffset.UtcNow.AddDays(30)
        });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Dashboard validation ──────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardSummary_FromGreaterThanTo_Returns400()
    {
        var sid = await ManagerSidAsync();
        var url = "/api/dashboard/summary?from=2027-01-01T00:00:00Z&to=2025-01-01T00:00:00Z";
        var response = await _client.SendAsync(Request(HttpMethod.Get, url, sid));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Error response shape ──────────────────────────────────────────────

    [Fact]
    public async Task ErrorResponse_HasStructuredBody()
    {
        // Hit a known validation error and verify the error body is structured
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name        = "X",
            phoneNumber = "invalid",
            password    = "TestPass1!",
            mallID      = TestSeeder.MallId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // API error responses must have an "error" wrapper with at least a "code"
        Assert.True(body.TryGetProperty("error", out var err),
            "Response body must contain an 'error' property");
        Assert.True(err.TryGetProperty("code", out _),
            "Error must contain a 'code' property");
    }
}
