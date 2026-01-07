using System.Net;
using System.Net.Http.Json;
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
/// Contract tests for QR Code API endpoints
/// Tests API contract compliance for GET /host/polls/{code}/qr
/// </summary>
public class QRCodeApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly System.Data.Common.DbConnection _connection;

    public QRCodeApiTests(WebApplicationFactory<Program> factory)
    {
        // Create persistent in-memory SQLite connection
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

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

                // Add DbContext with shared in-memory database
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
    }

    [Fact]
    public async Task GetQRCode_WithValidPollCode_ReturnsBase64Image()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Host-Token", "test-token");

        // Create a poll first
        var poll = await CreateTestPollAsync("Test QR Poll", new[] { "Yes", "No" });

        // Act
        var response = await client.GetAsync($"/host/polls/{poll.Code}/qr");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<QRCodeResponse>();
        result.Should().NotBeNull();
        result!.QrCodeDataUrl.Should().NotBeNullOrEmpty();
        result.QrCodeDataUrl.Should().StartWith("data:image/png;base64,");
        result.PollCode.Should().Be(poll.Code);
    }

    [Fact]
    public async Task GetQRCode_WithoutHostToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        // No host token provided

        // Act
        var response = await client.GetAsync("/host/polls/TEST/qr");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetQRCode_WithInvalidHostToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Host-Token", "wrong-token");

        // Act
        var response = await client.GetAsync("/host/polls/TEST/qr");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetQRCode_WithNonExistentPollCode_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Host-Token", "test-token");

        // Act
        var response = await client.GetAsync("/host/polls/XXXX/qr");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetQRCode_ResponseContainsCorrectContentType()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Host-Token", "test-token");

        var poll = await CreateTestPollAsync("Content Type Test", new[] { "A", "B" });

        // Act
        var response = await client.GetAsync($"/host/polls/{poll.Code}/qr");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task GetQRCode_ShouldIncludeAbsoluteUrl()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Host-Token", "test-token");

        var poll = await CreateTestPollAsync("URL Test", new[] { "Option1" });

        // Act
        var response = await client.GetAsync($"/host/polls/{poll.Code}/qr");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<QRCodeResponse>();
        result.Should().NotBeNull();
        result!.AbsoluteUrl.Should().NotBeNullOrEmpty();
        result.AbsoluteUrl.Should().Contain($"/p/{poll.Code}");
    }

    [Fact]
    public async Task GetQRCode_MultipleCalls_ShouldReturnConsistentResult()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Host-Token", "test-token");

        var poll = await CreateTestPollAsync("Consistency Test", new[] { "X", "Y" });

        // Act
        var response1 = await client.GetAsync($"/host/polls/{poll.Code}/qr");
        var result1 = await response1.Content.ReadFromJsonAsync<QRCodeResponse>();

        var response2 = await client.GetAsync($"/host/polls/{poll.Code}/qr");
        var result2 = await response2.Content.ReadFromJsonAsync<QRCodeResponse>();

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1!.QrCodeDataUrl.Should().Be(result2!.QrCodeDataUrl, "QR code should be consistent across calls");
    }

    private async Task<Poll> CreateTestPollAsync(string question, string[] optionTexts)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();

        var poll = new Poll
        {
            Code = Guid.NewGuid().ToString().Substring(0, 4).ToUpper(),
            Question = question,
            ChoiceMode = ChoiceMode.Single,
            IsClosed = false,
            CreatedAt = DateTime.UtcNow
        };

        context.Polls.Add(poll);
        await context.SaveChangesAsync();

        for (int i = 0; i < optionTexts.Length; i++)
        {
            var option = new Option
            {
                PollId = poll.Id,
                Text = optionTexts[i],
                DisplayOrder = i
            };
            context.Options.Add(option);
        }

        await context.SaveChangesAsync();
        return poll;
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}

/// <summary>
/// Response model for QR code endpoint
/// </summary>
public class QRCodeResponse
{
    public string PollCode { get; set; } = string.Empty;
    public string QrCodeDataUrl { get; set; } = string.Empty;
    public string AbsoluteUrl { get; set; } = string.Empty;
}
