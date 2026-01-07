using System.Data.Common;
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
/// Contract tests for Voting API endpoints
/// Tests vote submission, cookie handling, validation
/// </summary>
public class VotingApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DbConnection _connection;

    public VotingApiTests(WebApplicationFactory<Program> factory)
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
    }

    [Fact]
    public async Task SubmitVote_ShouldReturn200WithValidVote()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var poll = await CreateTestPollAsync("Test poll", new[] { "Red", "Blue", "Green" });
        var options = poll.Options!.ToList();

        var request = new
        {
            selectedOptionIds = new[] { options[0].Id }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/p/{poll.Code}/vote", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify voter ID cookie was set
        response.Headers.Should().ContainKey("Set-Cookie");
        var cookies = response.Headers.GetValues("Set-Cookie");
        cookies.Should().Contain(c => c.Contains("VoterId="));
    }

    [Fact]
    public async Task SubmitVote_ShouldReturn400WhenNoOptionSelected()
    {
        // Arrange
        var client = _factory.CreateClient();
        var poll = await CreateTestPollAsync("Test", new[] { "A", "B" });

        var request = new
        {
            selectedOptionIds = Array.Empty<int>()
        };

        // Act
        var response = await client.PostAsJsonAsync($"/p/{poll.Code}/vote", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitVote_ShouldReturn400WhenMultipleOptionsSelectedForSingleChoice()
    {
        // Arrange
        var client = _factory.CreateClient();
        var poll = await CreateTestPollAsync("Single choice", new[] { "A", "B", "C" }, ChoiceMode.Single);
        var options = poll.Options!.ToList();

        var request = new
        {
            selectedOptionIds = new[] { options[0].Id, options[1].Id }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/p/{poll.Code}/vote", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.ToLower().Should().Contain("single");
    }

    [Fact]
    public async Task SubmitVote_ShouldAcceptMultipleOptionsForMultiChoice()
    {
        // Arrange
        var client = _factory.CreateClient();
        var poll = await CreateTestPollAsync("Multi choice", new[] { "A", "B", "C", "D" }, ChoiceMode.Multi);
        var options = poll.Options!.ToList();

        var request = new
        {
            selectedOptionIds = new[] { options[0].Id, options[2].Id, options[3].Id }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/p/{poll.Code}/vote", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SubmitVote_ShouldReturn404WhenPollCodeDoesNotExist()
    {
        // Arrange
        var client = _factory.CreateClient();

        var request = new
        {
            selectedOptionIds = new[] { 999 }
        };

        // Act
        var response = await client.PostAsJsonAsync("/p/FAKE/vote", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmitVote_ShouldReturn400WhenPollIsClosed()
    {
        // Arrange
        var client = _factory.CreateClient();
        var poll = await CreateTestPollAsync("Closed poll", new[] { "A", "B" });
        
        // Close the poll
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();
        var pollToClose = await context.Polls.FindAsync(poll.Id);
        pollToClose!.IsClosed = true;
        pollToClose.ClosedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var options = poll.Options!.ToList();
        var request = new
        {
            selectedOptionIds = new[] { options[0].Id }
        };

        // Act
        var response = await client.PostAsJsonAsync($"/p/{poll.Code}/vote", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("closed");
    }

    private async Task<Poll> CreateTestPollAsync(string question, string[] optionTexts, ChoiceMode choiceMode = ChoiceMode.Single)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();

        var poll = new Poll
        {
            Code = Guid.NewGuid().ToString().Substring(0, 4).ToUpper(),
            Question = question,
            ChoiceMode = choiceMode,
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

        // Reload with options
        poll = await context.Polls
            .Include(p => p.Options)
            .FirstAsync(p => p.Id == poll.Id);

        return poll;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
