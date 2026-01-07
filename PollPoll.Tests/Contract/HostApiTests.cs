using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PollPoll.Data;
using PollPoll.Models;
using Xunit;

namespace PollPoll.Tests.Contract;

/// <summary>
/// Contract tests for Host API endpoints
/// Tests request validation, response schemas, and authentication
/// </summary>
public class HostApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly DbConnection _connection;

    public HostApiTests(WebApplicationFactory<Program> factory)
    {
        // Create persistent in-memory SQLite connection
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Enable WAL mode for better concurrency
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["HostAuth:Token"] = "test-token"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PollDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add DbContext with shared in-memory database using persistent connection
                services.AddDbContext<PollDbContext>(options =>
                    options.UseSqlite(_connection));
            });
        });

        // Ensure database is created
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PollDbContext>();
            db.Database.EnsureCreated();
        }

        _client = _factory.CreateClient();
        
        // Add host authentication token
        _client.DefaultRequestHeaders.Add("X-Host-Token", "test-token");
    }

    [Fact]
    public async Task CreatePoll_ShouldReturn201WithValidRequest()
    {
        // Arrange
        var request = new
        {
            question = "What's your favorite color?",
            choiceMode = "Single",
            options = new[]
            {
                new { text = "Red" },
                new { text = "Blue" },
                new { text = "Green" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/host/polls", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        result.GetProperty("pollId").GetInt32().Should().BeGreaterThan(0);
        result.GetProperty("code").GetString().Should().HaveLength(4);
        result.GetProperty("question").GetString().Should().Be("What's your favorite color?");
        result.GetProperty("choiceMode").GetString().Should().Be("Single");
        result.GetProperty("joinUrl").GetString().Should().Contain("/p/");
        result.GetProperty("qrCodeDataUrl").GetString().Should().StartWith("data:image/png;base64,");
    }

    [Fact]
    public async Task CreatePoll_ShouldReturn400WhenQuestionIsMissing()
    {
        // Arrange
        var request = new
        {
            choiceMode = "Single",
            options = new[]
            {
                new { text = "Option 1" },
                new { text = "Option 2" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/host/polls", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePoll_ShouldReturn400WhenOptionCountIsInvalid()
    {
        // Arrange - Only 1 option (minimum is 2)
        var request = new
        {
            question = "Test question",
            choiceMode = "Single",
            options = new[]
            {
                new { text = "Only option" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/host/polls", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("2-6 options");
    }

    [Fact]
    public async Task CreatePoll_ShouldReturn400WhenQuestionTooLong()
    {
        // Arrange
        var request = new
        {
            question = new string('A', 501), // Exceeds 500 char limit
            choiceMode = "Single",
            options = new[]
            {
                new { text = "Option 1" },
                new { text = "Option 2" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/host/polls", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePoll_ShouldReturn401WhenHostTokenMissing()
    {
        // Arrange
        var clientWithoutAuth = _factory.CreateClient();
        var request = new
        {
            question = "Test",
            choiceMode = "Single",
            options = new[]
            {
                new { text = "A" },
                new { text = "B" }
            }
        };

        // Act
        var response = await clientWithoutAuth.PostAsJsonAsync("/host/polls", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePoll_ShouldAcceptMultiChoiceMode()
    {
        // Arrange
        var request = new
        {
            question = "Select all that apply",
            choiceMode = "Multi",
            options = new[]
            {
                new { text = "Option 1" },
                new { text = "Option 2" },
                new { text = "Option 3" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/host/polls", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        result.GetProperty("choiceMode").GetString().Should().Be("Multi");
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
