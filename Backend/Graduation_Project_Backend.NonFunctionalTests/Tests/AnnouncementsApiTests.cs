using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.NonFunctionalTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.NonFunctionalTests.Tests;

/// <summary>
/// NF-04: Announcements API integration tests.
/// Covers get, create, update, status change, pin, and delete.
/// </summary>
public sealed class AnnouncementsApiTests : IClassFixture<NonFunctionalTestFactory>
{
    private readonly HttpClient _client;
    private string? _managerSid;
    private string? _userSid;

    public AnnouncementsApiTests(NonFunctionalTestFactory factory)
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

    private async Task<string> UserSidAsync()
    {
        if (_userSid != null) return _userSid;
        var phone = "+9627" + DateTimeOffset.UtcNow.Ticks.ToString()[^8..];
        var r     = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "AnnUser", phoneNumber = phone, password = "TestPass1!",
            mallID = TestSeeder.MallId
        });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return _userSid = body.GetProperty("sessionId").GetString()!;
    }

    private HttpRequestMessage Request(HttpMethod method, string url, string sid)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Session-Id", sid);
        return req;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<string> CreateAnnouncementAsync(string sid)
    {
        var req = Request(HttpMethod.Post, "/api/announcements", sid);
        req.Content = JsonContent.Create(new
        {
            title            = "NF Test Announcement",
            content          = "Created by NF tests",
            announcementType = "general",
            priority         = "normal",
            isActive         = true,
            isPinned         = false,
            startDate        = DateTimeOffset.UtcNow.AddDays(-1),
            endDate          = DateTimeOffset.UtcNow.AddDays(30)
        });
        var response = await _client.SendAsync(req);
        var body     = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetString()!;
    }

    // ── Tests ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAnnouncements_AsUser_Returns200WithArray()
    {
        var sid      = await UserSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/announcements", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    [Fact]
    public async Task CreateAnnouncement_AsManager_Returns201()
    {
        var sid = await ManagerSidAsync();
        var req = Request(HttpMethod.Post, "/api/announcements", sid);
        req.Content = JsonContent.Create(new
        {
            title            = "New Announcement",
            content          = "NF test content",
            announcementType = "news",
            priority         = "high",
            isActive         = true,
            isPinned         = false,
            startDate        = DateTimeOffset.UtcNow.AddDays(-1),
            endDate          = DateTimeOffset.UtcNow.AddDays(10)
        });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("New Announcement", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task UpdateAnnouncement_AsManager_Returns200WithNewTitle()
    {
        var sid = await ManagerSidAsync();
        var id  = await CreateAnnouncementAsync(sid);

        var req = Request(HttpMethod.Put, $"/api/announcements/{id}", sid);
        req.Content = JsonContent.Create(new
        {
            title            = "Updated Title",
            content          = "Updated content",
            announcementType = "general",
            priority         = "normal",
            isActive         = true,
            isPinned         = false,
            startDate        = DateTimeOffset.UtcNow.AddDays(-1),
            endDate          = DateTimeOffset.UtcNow.AddDays(30)
        });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Title", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task SetAnnouncementStatus_Deactivate_Returns200WithIsActiveFalse()
    {
        var sid = await ManagerSidAsync();
        var id  = await CreateAnnouncementAsync(sid);

        var req = Request(HttpMethod.Patch, $"/api/announcements/{id}/status", sid);
        req.Content = JsonContent.Create(new { isActive = false });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task PinAnnouncement_AsManager_Returns200WithIsPinnedTrue()
    {
        var sid = await ManagerSidAsync();
        var id  = await CreateAnnouncementAsync(sid);

        var req = Request(HttpMethod.Patch, $"/api/announcements/{id}/pin", sid);
        req.Content = JsonContent.Create(new { isPinned = true });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isPinned").GetBoolean());
    }

    [Fact]
    public async Task DeleteAnnouncement_AsManager_Returns204()
    {
        var sid = await ManagerSidAsync();
        var id  = await CreateAnnouncementAsync(sid);

        var response = await _client.SendAsync(
            Request(HttpMethod.Delete, $"/api/announcements/{id}", sid));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetManagedAnnouncements_AsManager_Returns200()
    {
        var sid      = await ManagerSidAsync();
        var response = await _client.SendAsync(
            Request(HttpMethod.Get, "/api/announcements/manage", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
