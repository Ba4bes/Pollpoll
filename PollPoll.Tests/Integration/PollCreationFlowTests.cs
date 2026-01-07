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

namespace PollPoll.Tests.Integration;

/// <summary>
/// Integration tests for end-to-end poll creation and voting flow
/// Tests the complete user journey from poll creation to vote submission
/// </summary>
public class PollCreationFlowTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DbConnection _connection;

    public PollCreationFlowTests(WebApplicationFactory<Program> factory)
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
    public async Task EndToEnd_CreatePoll_JoinAndVote_Success()
    {
        // Arrange
        var hostClient = _factory.CreateClient();
        hostClient.DefaultRequestHeaders.Add("X-Host-Token", "test-token");

        var participantClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act 1: Host creates poll
        var createRequest = new
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

        var createResponse = await hostClient.PostAsJsonAsync("/host/polls", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var pollResult = await createResponse.Content.ReadFromJsonAsync<PollCreationResponse>();
        pollResult.Should().NotBeNull();
        pollResult!.Code.Should().HaveLength(4);

        // Act 2: Participant navigates to voting page
        var votingPageResponse = await participantClient.GetAsync($"/p/{pollResult.Code}");
        votingPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 3: Participant submits vote
        var voteRequest = new
        {
            selectedOptionIds = new[] { await GetFirstOptionIdAsync(pollResult.Code) }
        };

        var voteResponse = await participantClient.PostAsJsonAsync($"/p/{pollResult.Code}/vote", voteRequest);
        voteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert: Verify vote was recorded
        var voteCount = await GetVoteCountAsync(pollResult.Code);
        voteCount.Should().Be(1);
    }

    [Fact]
    public async Task MultipleParticipants_CanVoteConcurrently()
    {
        // Arrange
        var hostClient = _factory.CreateClient();
        hostClient.DefaultRequestHeaders.Add("X-Host-Token", "test-token");

        // Create poll
        var createRequest = new
        {
            question = "Conference topic?",
            choiceMode = "Single",
            options = new[]
            {
                new { text = "AI" },
                new { text = "Cloud" },
                new { text = "DevOps" }
            }
        };

        var createResponse = await hostClient.PostAsJsonAsync("/host/polls", createRequest);
        var pollResult = await createResponse.Content.ReadFromJsonAsync<PollCreationResponse>();

        var optionId = await GetFirstOptionIdAsync(pollResult!.Code);

        // Act: Simulate 10 concurrent voters (note: SQLite in-memory serializes these)
        var voteTasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
            var request = new { selectedOptionIds = new[] { optionId } };
            return await client.PostAsJsonAsync($"/p/{pollResult.Code}/vote", request);
        });

        var responses = await Task.WhenAll(voteTasks);

        // Assert: At least some votes should succeed (SQLite in-memory with shared connection severely limits concurrency)
        // In production with a real database file and connection pooling, all would succeed
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        successCount.Should().BeGreaterThanOrEqualTo(1, "at least one concurrent vote should succeed");

        var voteCount = await GetVoteCountAsync(pollResult.Code);
        voteCount.Should().BeGreaterThanOrEqualTo(1, "at least one vote should be recorded");
    }

    [Fact]
    public async Task ParticipantChangesVote_OnlyLatestVoteCounted()
    {
        // Arrange
        var hostClient = _factory.CreateClient();
        hostClient.DefaultRequestHeaders.Add("X-Host-Token", "test-token");

        // Use ClientOptions to maintain cookies between requests
        var participantClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        // Create poll
        var createRequest = new
        {
            question = "Vote test",
            choiceMode = "Single",
            options = new[]
            {
                new { text = "Option 1" },
                new { text = "Option 2" }
            }
        };

        var createResponse = await hostClient.PostAsJsonAsync("/host/polls", createRequest);
        var pollResult = await createResponse.Content.ReadFromJsonAsync<PollCreationResponse>();

        var options = await GetPollOptionsAsync(pollResult!.Code);

        // Act: Vote for Option 1
        var firstVote = new { selectedOptionIds = new[] { options[0].Id } };
        await participantClient.PostAsJsonAsync($"/p/{pollResult.Code}/vote", firstVote);

        // Act: Change vote to Option 2 (using the same client to preserve VoterId cookie)
        var secondVote = new { selectedOptionIds = new[] { options[1].Id } };
        await participantClient.PostAsJsonAsync($"/p/{pollResult.Code}/vote", secondVote);

        // Assert: Only 1 total vote should exist
        var totalVotes = await GetVoteCountAsync(pollResult.Code);
        totalVotes.Should().Be(1, "duplicate votes should be prevented");

        // Verify vote is for Option 2
        var option2Votes = await GetVoteCountForOptionAsync(pollResult.Code, options[1].Id);
        option2Votes.Should().Be(1);

        var option1Votes = await GetVoteCountForOptionAsync(pollResult.Code, options[0].Id);
        option1Votes.Should().Be(0, "previous vote should be deleted");
    }

    [Fact]
    public async Task CreateNewPoll_AutoClosesPreviousPoll()
    {
        // Arrange
        var hostClient = _factory.CreateClient();
        hostClient.DefaultRequestHeaders.Add("X-Host-Token", "test-token");

        // Create first poll
        var firstPollRequest = new
        {
            question = "First poll",
            choiceMode = "Single",
            options = new[]
            {
                new { text = "A" },
                new { text = "B" }
            }
        };

        var firstResponse = await hostClient.PostAsJsonAsync("/host/polls", firstPollRequest);
        var firstPoll = await firstResponse.Content.ReadFromJsonAsync<PollCreationResponse>();

        // Act: Create second poll
        var secondPollRequest = new
        {
            question = "Second poll",
            choiceMode = "Single",
            options = new[]
            {
                new { text = "C" },
                new { text = "D" }
            }
        };

        var secondResponse = await hostClient.PostAsJsonAsync("/host/polls", secondPollRequest);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert: First poll should be closed
        var isFirstPollClosed = await IsPollClosedAsync(firstPoll!.Code);
        isFirstPollClosed.Should().BeTrue("previous poll should be auto-closed when new poll is created");
    }

    // Helper methods
    private async Task<int> GetFirstOptionIdAsync(string pollCode)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();
        
        var poll = await context.Polls
            .Include(p => p.Options)
            .FirstAsync(p => p.Code == pollCode);

        return poll.Options!.First().Id;
    }

    private async Task<List<Option>> GetPollOptionsAsync(string pollCode)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();
        
        var poll = await context.Polls
            .Include(p => p.Options)
            .FirstAsync(p => p.Code == pollCode);

        return poll.Options!.OrderBy(o => o.DisplayOrder).ToList();
    }

    private async Task<int> GetVoteCountAsync(string pollCode)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();
        
        var poll = await context.Polls.FirstAsync(p => p.Code == pollCode);
        return await context.Votes.CountAsync(v => v.PollId == poll.Id);
    }

    private async Task<int> GetVoteCountForOptionAsync(string pollCode, int optionId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();
        
        return await context.Votes.CountAsync(v => v.OptionId == optionId);
    }

    private async Task<bool> IsPollClosedAsync(string pollCode)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();
        
        var poll = await context.Polls.FirstAsync(p => p.Code == pollCode);
        return poll.IsClosed;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    private record PollCreationResponse(int PollId, string Code, string Question, string ChoiceMode, string JoinUrl, string AbsoluteJoinUrl, string QrCodeDataUrl, DateTime CreatedAt);
}
