using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.IntegrationTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Announcements CRUD against real PostgreSQL.
/// </summary>
[Collection("Integration")]
public sealed class AnnouncementsIntegrationTests
{
    private readonly HttpClient _client;
    private string? _userSid;
    private string? _managerSid;

    public AnnouncementsIntegrationTests(IntegrationTestFactory factory)
    {
        _client = factory.CreateClient();
        using var db = factory.CreateDbContext();
        IntegrationTestSeeder.Seed(db);
    }

    private async Task<string> UserSidAsync()
    {
        if (_userSid != null) return _userSid;
        var phone = "+9627" + DateTimeOffset.UtcNow.Ticks.ToString()[^8..];
        var r = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "AnnouncUser", phoneNumber = phone,
            password = "TestPass1!", mallID = IntegrationTestSeeder.MallId
        });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return _userSid = body.GetProperty("sessionId").GetString()!;
    }

    private async Task<string> ManagerSidAsync()
    {
        if (_managerSid != null) return _managerSid;
        var r = await _client.PostAsJsonAsync("/api/auth/manager-quick-login",
            new { managerId = IntegrationTestSeeder.ManagerId });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return _managerSid = body.GetProperty("sessionId").GetString()!;
    }

    private HttpRequestMessage Request(HttpMethod m, string url, string sid)
    {
        var req = new HttpRequestMessage(m, url);
        req.Headers.Add("X-Session-Id", sid);
        return req;
    }

    [Fact]
    public async Task GetAnnouncements_AsUser_Returns200_WithSeededData()
    {
        var sid      = await UserSidAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/announcements", sid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.True(body.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task CreateAnnouncement_AsManager_Returns201_AndSavedToDb()
    {
        var sid = await ManagerSidAsync();
        var req = Request(HttpMethod.Post, "/api/announcements", sid);
        req.Content = JsonContent.Create(new
        {
            title            = "Integration Test Announcement",
            content          = "This is a test announcement from integration tests.",
            announcementType = "general",
            priority         = "normal",
            isActive         = true,
            isPinned         = false,
            startDate        = DateTimeOffset.UtcNow.AddDays(-1),
            endDate          = DateTimeOffset.UtcNow.AddDays(30)
        });

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Integration Test Announcement", body.GetProperty("title").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task UpdateAnnouncement_AsManager_Returns200_WithUpdatedTitle()
    {
        var sid = await ManagerSidAsync();

        // Create
        var create = Request(HttpMethod.Post, "/api/announcements", sid);
        create.Content = JsonContent.Create(new
        {
            title = "Announcement To Update", content = "Content",
            announcementType = "general", priority = "normal",
            isActive = true, isPinned = false,
            startDate = DateTimeOffset.UtcNow, endDate = DateTimeOffset.UtcNow.AddDays(30)
        });
        var created    = await _client.SendAsync(create);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id          = createdBody.GetProperty("id").GetString()!;

        // Update
        var update = Request(HttpMethod.Put, $"/api/announcements/{id}", sid);
        update.Content = JsonContent.Create(new
        {
            title = "Updated Announcement", content = "Updated content",
            announcementType = "news", priority = "high",
            isActive = true, isPinned = false,
            startDate = DateTimeOffset.UtcNow, endDate = DateTimeOffset.UtcNow.AddDays(30)
        });
        var response = await _client.SendAsync(update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Announcement", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task SetAnnouncementStatus_Deactivate_Returns200_IsActiveFalse()
    {
        var sid = await ManagerSidAsync();

        // Create active announcement
        var create = Request(HttpMethod.Post, "/api/announcements", sid);
        create.Content = JsonContent.Create(new
        {
            title = "Active Announcement", content = "Content",
            announcementType = "general", priority = "normal",
            isActive = true, isPinned = false,
            startDate = DateTimeOffset.UtcNow, endDate = DateTimeOffset.UtcNow.AddDays(30)
        });
        var created = await _client.SendAsync(create);
        var id      = (await created.Content.ReadFromJsonAsync<JsonElement>())
                        .GetProperty("id").GetString()!;

        // Deactivate
        var patch = Request(HttpMethod.Patch, $"/api/announcements/{id}/status", sid);
        patch.Content = JsonContent.Create(new { isActive = false });
        var response  = await _client.SendAsync(patch);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task DeleteAnnouncement_AsManager_Returns204()
    {
        var sid = await ManagerSidAsync();

        // Create
        var create = Request(HttpMethod.Post, "/api/announcements", sid);
        create.Content = JsonContent.Create(new
        {
            title = "Announcement To Delete", content = "Content",
            announcementType = "general", priority = "normal",
            isActive = true, isPinned = false,
            startDate = DateTimeOffset.UtcNow, endDate = DateTimeOffset.UtcNow.AddDays(30)
        });
        var created = await _client.SendAsync(create);
        var id      = (await created.Content.ReadFromJsonAsync<JsonElement>())
                        .GetProperty("id").GetString()!;

        // Delete
        var response = await _client.SendAsync(
            Request(HttpMethod.Delete, $"/api/announcements/{id}", sid));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
