using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PollPoll.Data;
using PollPoll.Models;
using System.Data.Common;
using System.Net;
using System.Text.Json;

namespace PollPoll.Tests.Contract;

public class ResultsApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly DbConnection _connection;

    public ResultsApiTests(WebApplicationFactory<Program> factory)
    {
        // Create persistent SQLite in-memory connection
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
            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PollDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add DbContext using the shared in-memory connection
                services.AddDbContext<PollDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });

                // Ensure database is created
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PollDbContext>();
                db.Database.EnsureCreated();
            });
        });

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetResults_ValidCode_ReturnsJsonWithCorrectSchema()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();

        var poll = new Poll
        {
            Code = "TEST",
            Question = "Favorite color?",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = false
        };
        context.Polls.Add(poll);
        await context.SaveChangesAsync();

        var option1 = new Option { PollId = poll.Id, Text = "Red", DisplayOrder = 0 };
        var option2 = new Option { PollId = poll.Id, Text = "Blue", DisplayOrder = 1 };
        context.Options.AddRange(option1, option2);
        await context.SaveChangesAsync();

        // Add 3 votes for Red, 2 for Blue
        context.Votes.AddRange(
            new Vote { PollId = poll.Id, OptionId = option1.Id, VoterId = Guid.NewGuid() },
            new Vote { PollId = poll.Id, OptionId = option1.Id, VoterId = Guid.NewGuid() },
            new Vote { PollId = poll.Id, OptionId = option1.Id, VoterId = Guid.NewGuid() },
            new Vote { PollId = poll.Id, OptionId = option2.Id, VoterId = Guid.NewGuid() },
            new Vote { PollId = poll.Id, OptionId = option2.Id, VoterId = Guid.NewGuid() }
        );
        await context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/api/results/TEST");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        // Verify schema
        root.GetProperty("pollCode").GetString().Should().Be("TEST");
        root.GetProperty("question").GetString().Should().Be("Favorite color?");
        root.GetProperty("isClosed").GetBoolean().Should().BeFalse();
        root.GetProperty("totalVotes").GetInt32().Should().Be(5);

        var options = root.GetProperty("options");
        options.GetArrayLength().Should().Be(2);

        var redOption = options.EnumerateArray().First(o => o.GetProperty("text").GetString() == "Red");
        redOption.GetProperty("optionId").GetInt32().Should().Be(option1.Id);
        redOption.GetProperty("voteCount").GetInt32().Should().Be(3);
        redOption.GetProperty("percentage").GetDouble().Should().BeApproximately(60.0, 0.1);

        var blueOption = options.EnumerateArray().First(o => o.GetProperty("text").GetString() == "Blue");
        blueOption.GetProperty("optionId").GetInt32().Should().Be(option2.Id);
        blueOption.GetProperty("voteCount").GetInt32().Should().Be(2);
        blueOption.GetProperty("percentage").GetDouble().Should().BeApproximately(40.0, 0.1);
    }

    [Fact]
    public async Task GetResults_InvalidCode_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/api/results/NOPE");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetResults_CaseInsensitive_FindsPoll()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();

        var poll = new Poll
        {
            Code = "ABCD",
            Question = "Test",
            ChoiceMode = ChoiceMode.Single
        };
        context.Polls.Add(poll);
        await context.SaveChangesAsync();

        var option = new Option { PollId = poll.Id, Text = "Option", DisplayOrder = 0 };
        context.Options.Add(option);
        await context.SaveChangesAsync();

        // Act
        var response1 = await _client.GetAsync("/api/results/abcd");
        var response2 = await _client.GetAsync("/api/results/ABCD");
        var response3 = await _client.GetAsync("/api/results/AbCd");

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        response3.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetResults_ClosedPoll_IncludesClosedFlag()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();

        var poll = new Poll
        {
            Code = "SHUT",
            Question = "Closed poll",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = true,
            ClosedAt = DateTime.UtcNow
        };
        context.Polls.Add(poll);
        await context.SaveChangesAsync();

        var option = new Option { PollId = poll.Id, Text = "Option", DisplayOrder = 0 };
        context.Options.Add(option);
        await context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/api/results/SHUT");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        
        json.RootElement.GetProperty("isClosed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetResults_NoVotes_ReturnsZeroCountsAndPercentages()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();

        var poll = new Poll
        {
            Code = "ZERO",
            Question = "No votes",
            ChoiceMode = ChoiceMode.Single
        };
        context.Polls.Add(poll);
        await context.SaveChangesAsync();

        var option = new Option { PollId = poll.Id, Text = "Option", DisplayOrder = 0 };
        context.Options.Add(option);
        await context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/api/results/ZERO");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        
        json.RootElement.GetProperty("totalVotes").GetInt32().Should().Be(0);
        
        var options = json.RootElement.GetProperty("options");
        var firstOption = options.EnumerateArray().First();
        firstOption.GetProperty("voteCount").GetInt32().Should().Be(0);
        firstOption.GetProperty("percentage").GetDouble().Should().Be(0.0);
    }

    [Fact]
    public async Task GetResults_OptionsOrderedByDisplayOrder()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();

        var poll = new Poll
        {
            Code = "SORT",
            Question = "Order test",
            ChoiceMode = ChoiceMode.Single
        };
        context.Polls.Add(poll);
        await context.SaveChangesAsync();

        // Add options in non-sequential order
        var option3 = new Option { PollId = poll.Id, Text = "Third", DisplayOrder = 2 };
        var option1 = new Option { PollId = poll.Id, Text = "First", DisplayOrder = 0 };
        var option2 = new Option { PollId = poll.Id, Text = "Second", DisplayOrder = 1 };
        context.Options.AddRange(option3, option1, option2);
        await context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/api/results/SORT");

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var options = json.RootElement.GetProperty("options").EnumerateArray().ToList();

        options[0].GetProperty("text").GetString().Should().Be("First");
        options[1].GetProperty("text").GetString().Should().Be("Second");
        options[2].GetProperty("text").GetString().Should().Be("Third");
    }
}
