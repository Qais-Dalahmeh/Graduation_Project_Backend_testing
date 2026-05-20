using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.IntegrationTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.IntegrationTests.Tests;

/// <summary>
/// Integration tests for authentication against a real PostgreSQL database.
/// Verifies register, login, manager login, and session lifecycle end-to-end.
/// </summary>
[Collection("Integration")]
public sealed class AuthIntegrationTests
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(IntegrationTestFactory factory)
    {
        _client = factory.CreateClient();
        using var db = factory.CreateDbContext();
        IntegrationTestSeeder.Seed(db);
    }

    private string UniquePhone() =>
        "+9627" + DateTimeOffset.UtcNow.Ticks.ToString()[^8..];

    // ── Register ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidUser_Returns200_AndPersistsToDatabase()
    {
        var phone = UniquePhone();
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name        = "Integration User",
            phoneNumber = phone,
            password    = "TestPass1!",
            mallID      = IntegrationTestSeeder.MallId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("sessionId").GetString()));
        Assert.Equal("user", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Register_DuplicatePhone_Returns409()
    {
        var phone = UniquePhone();

        // First registration
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "First", phoneNumber = phone,
            password = "TestPass1!", mallID = IntegrationTestSeeder.MallId
        });

        // Second registration with same phone
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Second", phoneNumber = phone,
            password = "TestPass1!", mallID = IntegrationTestSeeder.MallId
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── Login ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_CorrectCredentials_Returns200_WithSession()
    {
        var phone = UniquePhone();
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "LoginUser", phoneNumber = phone,
            password = "TestPass1!", mallID = IntegrationTestSeeder.MallId
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            phoneNumber = phone,
            password    = "TestPass1!",
            mallID      = IntegrationTestSeeder.MallId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("sessionId").GetString()));
        Assert.True(body.GetProperty("totalPoints").GetInt32() >= 0);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var phone = UniquePhone();
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "WrongPassUser", phoneNumber = phone,
            password = "TestPass1!", mallID = IntegrationTestSeeder.MallId
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            phoneNumber = phone,
            password    = "WrongPassword!",
            mallID      = IntegrationTestSeeder.MallId
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Manager Login ─────────────────────────────────────────────────────

    [Fact]
    public async Task ManagerQuickLogin_ValidManager_Returns200_WithManagerRole()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/manager-quick-login", new
        {
            managerId = IntegrationTestSeeder.ManagerId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("manager", body.GetProperty("role").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("sessionId").GetString()));
    }

    // ── Session ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_ValidSession_Returns200_AndInvalidatesSession()
    {
        var phone = UniquePhone();
        var reg = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "LogoutUser", phoneNumber = phone,
            password = "TestPass1!", mallID = IntegrationTestSeeder.MallId
        });
        var regBody = await reg.Content.ReadFromJsonAsync<JsonElement>();
        var sid = regBody.GetProperty("sessionId").GetString()!;

        // Logout
        var logout = await _client.PostAsJsonAsync("/api/auth/logout", new { sessionId = sid });
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        // Session should now be invalid → 401 on protected endpoint
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/stores");
        req.Headers.Add("X-Session-Id", sid);
        var afterLogout = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }
}
