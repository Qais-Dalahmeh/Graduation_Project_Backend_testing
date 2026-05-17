using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Graduation_Project_Backend.NonFunctionalTests.Infrastructure;
using Xunit;

namespace Graduation_Project_Backend.NonFunctionalTests.Tests;

/// <summary>
/// NF-01: Auth endpoint integration tests.
/// Covers register, login, manager login, wrong-password, and logout flows.
/// </summary>
public sealed class AuthApiTests : IClassFixture<NonFunctionalTestFactory>
{
    private readonly HttpClient _client;
    private readonly NonFunctionalTestFactory _factory;

    public AuthApiTests(NonFunctionalTestFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();

        using var db = factory.CreateDbContext();
        TestSeeder.Seed(db);
    }

    // ── Register ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidUser_Returns200WithSession()
    {
        var phone = "+9627" + DateTimeOffset.UtcNow.Ticks.ToString()[^8..];

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name        = "Test User",
            phoneNumber = phone,
            password    = "TestPass1!",
            mallID      = TestSeeder.MallId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("sessionId", out var sid));
        Assert.NotEmpty(sid.GetString()!);
        Assert.Equal("user", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Register_DuplicatePhone_Returns409()
    {
        const string phone = "+962799111111";

        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "First", phoneNumber = phone, password = "TestPass1!", mallID = TestSeeder.MallId
        });

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Second", phoneNumber = phone, password = "TestPass1!", mallID = TestSeeder.MallId
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── Login ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_CorrectCredentials_Returns200WithSession()
    {
        const string phone = "+962799222222";
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Login Tester", phoneNumber = phone, password = "TestPass1!", mallID = TestSeeder.MallId
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            phoneNumber = phone, password = "TestPass1!", mallID = TestSeeder.MallId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEmpty(body.GetProperty("sessionId").GetString()!);
        Assert.True(body.GetProperty("totalPoints").GetInt32() >= 0);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        const string phone = "+962799333333";
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "WrongPass User", phoneNumber = phone, password = "TestPass1!", mallID = TestSeeder.MallId
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            phoneNumber = phone, password = "WrongPassword!", mallID = TestSeeder.MallId
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Manager Quick Login ───────────────────────────────────────────────

    [Fact]
    public async Task ManagerQuickLogin_ValidManagerId_Returns200WithManagerRole()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/manager-quick-login", new
        {
            managerId = TestSeeder.ManagerId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("manager", body.GetProperty("role").GetString());
        Assert.NotEmpty(body.GetProperty("sessionId").GetString()!);
    }

    [Fact]
    public async Task ManagerQuickLogin_UnknownManagerId_Returns4xx()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/manager-quick-login", new
        {
            managerId = Guid.NewGuid()
        });

        // API returns 400 (Bad Request) when manager ID is not found
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 400 or 404, got {(int)response.StatusCode}");
    }

    // ── Logout ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_ValidSession_Returns200()
    {
        const string phone = "+962799444444";
        var reg = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Logout Tester", phoneNumber = phone, password = "TestPass1!", mallID = TestSeeder.MallId
        });
        var regBody = await reg.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = regBody.GetProperty("sessionId").GetString()!;

        var response = await _client.PostAsJsonAsync("/api/auth/logout", new { sessionId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
