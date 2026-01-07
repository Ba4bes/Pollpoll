using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PollPoll.Data;
using Xunit;

namespace PollPoll.Tests.Integration;

/// <summary>
/// Integration tests for host authentication on Razor Pages
/// Tests that host dashboard requires authentication while other pages remain public
/// </summary>
public class HostAuthenticationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly System.Data.Common.DbConnection _connection;
    private const string ValidToken = "test-host-token";

    public HostAuthenticationTests(WebApplicationFactory<Program> factory)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["HostAuth:Token"] = ValidToken
                });
            });

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<PollDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<PollDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });
            });
        });

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PollDbContext>();
        dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Fact]
    public async Task HostDashboard_WithoutAuthentication_RedirectsToLogin()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/Login");
    }

    [Fact]
    public async Task LoginPage_WithValidToken_RedirectsToDashboard()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Get the login page first to get CSRF token
        var loginPageResponse = await client.GetAsync("/Login");
        var content = await loginPageResponse.Content.ReadAsStringAsync();

        // Extract the request verification token
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            content,
            @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)"""
        );
        var verificationToken = tokenMatch.Success ? tokenMatch.Groups[1].Value : "";

        // Act - Submit login form with valid token
        var formData = new Dictionary<string, string>
        {
            ["HostToken"] = ValidToken,
            ["__RequestVerificationToken"] = verificationToken
        };
        var formContent = new FormUrlEncodedContent(formData);
        var response = await client.PostAsync("/Login", formContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Be("/");
    }

    [Fact]
    public async Task LoginPage_WithInvalidToken_ShowsError()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Get the login page first
        var loginPageResponse = await client.GetAsync("/Login");
        var content = await loginPageResponse.Content.ReadAsStringAsync();

        // Extract CSRF token
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            content,
            @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)"""
        );
        var verificationToken = tokenMatch.Success ? tokenMatch.Groups[1].Value : "";

        // Act - Submit with invalid token
        var formData = new Dictionary<string, string>
        {
            ["HostToken"] = "invalid-token",
            ["__RequestVerificationToken"] = verificationToken
        };
        var formContent = new FormUrlEncodedContent(formData);
        var response = await client.PostAsync("/Login", formContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Invalid host token");
    }

    [Fact]
    public async Task HostDashboard_AfterLogin_IsAccessible()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Login first
        var loginPageResponse = await client.GetAsync("/Login");
        var loginContent = await loginPageResponse.Content.ReadAsStringAsync();

        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            loginContent,
            @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)"""
        );
        var verificationToken = tokenMatch.Success ? tokenMatch.Groups[1].Value : "";

        var formData = new Dictionary<string, string>
        {
            ["HostToken"] = ValidToken,
            ["__RequestVerificationToken"] = verificationToken
        };
        var formContent = new FormUrlEncodedContent(formData);
        await client.PostAsync("/Login", formContent);

        // Act - Access dashboard after login
        var response = await client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Host Dashboard");
    }

    [Fact]
    public async Task LogoutPage_ClearsSession_RedirectsToLogin()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Login first
        var loginPageResponse = await client.GetAsync("/Login");
        var loginContent = await loginPageResponse.Content.ReadAsStringAsync();

        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            loginContent,
            @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)"""
        );
        var verificationToken = tokenMatch.Success ? tokenMatch.Groups[1].Value : "";

        var formData = new Dictionary<string, string>
        {
            ["HostToken"] = ValidToken,
            ["__RequestVerificationToken"] = verificationToken
        };
        await client.PostAsync("/Login", new FormUrlEncodedContent(formData));

        // Act - Logout
        var logoutResponse = await client.GetAsync("/Logout");

        // Assert - Should redirect to login
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        logoutResponse.Headers.Location?.ToString().Should().Be("/Login");

        // Verify session is cleared - accessing dashboard should redirect to login
        var dashboardResponse = await client.GetAsync("/");
        dashboardResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        dashboardResponse.Headers.Location?.ToString().Should().Contain("/Login");
    }

    [Theory]
    [InlineData("/Privacy")]
    [InlineData("/Error")]
    public async Task PublicPages_DoNotRequireAuthentication(string url)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResultsPage_WithoutAuthentication_RedirectsToLogin()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Create a poll first (using authenticated API)
        var apiClient = _factory.CreateClient();
        apiClient.DefaultRequestHeaders.Add("X-Host-Token", ValidToken);

        var createResponse = await apiClient.PostAsJsonAsync("/host/polls", new
        {
            Question = "Test Question?",
            ChoiceMode = "Single",
            Options = new[] { new { Text = "Option A" }, new { Text = "Option B" } }
        });

        var pollData = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var code = pollData?["code"]?.ToString();

        // Act - Access results page without authentication
        var response = await client.GetAsync($"/p/{code}/results");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/Login");
    }

    [Fact]
    public async Task ResultsPage_WithAuthentication_IsAccessible()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Login first
        var loginPageResponse = await client.GetAsync("/Login");
        var loginContent = await loginPageResponse.Content.ReadAsStringAsync();

        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            loginContent,
            @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)"""
        );
        var verificationToken = tokenMatch.Success ? tokenMatch.Groups[1].Value : "";

        var formData = new Dictionary<string, string>
        {
            ["HostToken"] = ValidToken,
            ["__RequestVerificationToken"] = verificationToken
        };
        await client.PostAsync("/Login", new FormUrlEncodedContent(formData));

        // Create a poll (using authenticated API)
        var apiClient = _factory.CreateClient();
        apiClient.DefaultRequestHeaders.Add("X-Host-Token", ValidToken);

        var createResponse = await apiClient.PostAsJsonAsync("/host/polls", new
        {
            Question = "Test Question?",
            ChoiceMode = "Single",
            Options = new[] { new { Text = "Option A" }, new { Text = "Option B" } }
        });

        var pollData = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var code = pollData?["code"]?.ToString();

        // Act - Access results page with authentication
        var response = await client.GetAsync($"/p/{code}/results");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Test Question?");
    }

    [Fact]
    public async Task VotePage_IsPublic_NoAuthenticationRequired()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Create a poll first
        var apiClient = _factory.CreateClient();
        apiClient.DefaultRequestHeaders.Add("X-Host-Token", ValidToken);

        var createResponse = await apiClient.PostAsJsonAsync("/host/polls", new
        {
            Question = "Test Question?",
            ChoiceMode = "Single",
            Options = new[] { new { Text = "Option A" }, new { Text = "Option B" } }
        });

        var pollData = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var code = pollData?["code"]?.ToString();

        // Act - Access voting page without authentication
        var response = await client.GetAsync($"/p/{code}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Test Question?");
    }
}
