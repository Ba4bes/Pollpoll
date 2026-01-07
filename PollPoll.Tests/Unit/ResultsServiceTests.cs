using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using PollPoll.Data;
using PollPoll.Models;
using PollPoll.Services;
using System.Data.Common;

namespace PollPoll.Tests.Unit;

public class ResultsServiceTests : IDisposable
{
    private readonly DbConnection _connection;
    private readonly PollDbContext _context;
    private readonly ResultsService _resultsService;

    public ResultsServiceTests()
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

        var options = new DbContextOptionsBuilder<PollDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new PollDbContext(options);
        _context.Database.EnsureCreated();

        _resultsService = new ResultsService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetPollResults_ValidCode_ReturnsCorrectVoteCounts()
    {
        // Arrange
        var poll = new Poll
        {
            Code = "TEST",
            Question = "Favorite color?",
            ChoiceMode = ChoiceMode.Single
        };
        _context.Polls.Add(poll);
        await _context.SaveChangesAsync();

        var option1 = new Option { PollId = poll.Id, Text = "Red", DisplayOrder = 0 };
        var option2 = new Option { PollId = poll.Id, Text = "Blue", DisplayOrder = 1 };
        var option3 = new Option { PollId = poll.Id, Text = "Green", DisplayOrder = 2 };
        _context.Options.AddRange(option1, option2, option3);
        await _context.SaveChangesAsync();

        // Add votes: 3 for Red, 2 for Blue, 0 for Green
        var voterId1 = Guid.NewGuid();
        var voterId2 = Guid.NewGuid();
        var voterId3 = Guid.NewGuid();
        var voterId4 = Guid.NewGuid();
        var voterId5 = Guid.NewGuid();

        _context.Votes.AddRange(
            new Vote { PollId = poll.Id, OptionId = option1.Id, VoterId = voterId1 },
            new Vote { PollId = poll.Id, OptionId = option1.Id, VoterId = voterId2 },
            new Vote { PollId = poll.Id, OptionId = option1.Id, VoterId = voterId3 },
            new Vote { PollId = poll.Id, OptionId = option2.Id, VoterId = voterId4 },
            new Vote { PollId = poll.Id, OptionId = option2.Id, VoterId = voterId5 }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _resultsService.GetPollResults("TEST");

        // Assert
        result.Should().NotBeNull();
        result.PollCode.Should().Be("TEST");
        result.Question.Should().Be("Favorite color?");
        result.TotalVotes.Should().Be(5);
        result.Options.Should().HaveCount(3);

        var redOption = result.Options.First(o => o.Text == "Red");
        redOption.VoteCount.Should().Be(3);

        var blueOption = result.Options.First(o => o.Text == "Blue");
        blueOption.VoteCount.Should().Be(2);

        var greenOption = result.Options.First(o => o.Text == "Green");
        greenOption.VoteCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPollResults_ValidCode_CalculatesCorrectPercentages()
    {
        // Arrange
        var poll = new Poll
        {
            Code = "MATH",
            Question = "Test percentages",
            ChoiceMode = ChoiceMode.Single
        };
        _context.Polls.Add(poll);
        await _context.SaveChangesAsync();

        var option1 = new Option { PollId = poll.Id, Text = "A", DisplayOrder = 0 };
        var option2 = new Option { PollId = poll.Id, Text = "B", DisplayOrder = 1 };
        _context.Options.AddRange(option1, option2);
        await _context.SaveChangesAsync();

        // Add 3 votes for A, 2 votes for B (total 5)
        // Expected: A=60%, B=40%
        _context.Votes.AddRange(
            new Vote { PollId = poll.Id, OptionId = option1.Id, VoterId = Guid.NewGuid() },
            new Vote { PollId = poll.Id, OptionId = option1.Id, VoterId = Guid.NewGuid() },
            new Vote { PollId = poll.Id, OptionId = option1.Id, VoterId = Guid.NewGuid() },
            new Vote { PollId = poll.Id, OptionId = option2.Id, VoterId = Guid.NewGuid() },
            new Vote { PollId = poll.Id, OptionId = option2.Id, VoterId = Guid.NewGuid() }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _resultsService.GetPollResults("MATH");

        // Assert
        result.TotalVotes.Should().Be(5);
        
        var optionA = result.Options.First(o => o.Text == "A");
        optionA.Percentage.Should().BeApproximately(60.0, 0.1);

        var optionB = result.Options.First(o => o.Text == "B");
        optionB.Percentage.Should().BeApproximately(40.0, 0.1);
    }

    [Fact]
    public async Task GetPollResults_NoVotes_ReturnsZeroPercentages()
    {
        // Arrange
        var poll = new Poll
        {
            Code = "ZERO",
            Question = "No votes yet",
            ChoiceMode = ChoiceMode.Single
        };
        _context.Polls.Add(poll);
        await _context.SaveChangesAsync();

        var option1 = new Option { PollId = poll.Id, Text = "A", DisplayOrder = 0 };
        var option2 = new Option { PollId = poll.Id, Text = "B", DisplayOrder = 1 };
        _context.Options.AddRange(option1, option2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _resultsService.GetPollResults("ZERO");

        // Assert
        result.TotalVotes.Should().Be(0);
        result.Options.Should().AllSatisfy(o =>
        {
            o.VoteCount.Should().Be(0);
            o.Percentage.Should().Be(0.0);
        });
    }

    [Fact]
    public async Task GetPollResults_InvalidCode_ReturnsNull()
    {
        // Act
        var result = await _resultsService.GetPollResults("NOPE");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPollResults_CaseInsensitiveCode_FindsPoll()
    {
        // Arrange
        var poll = new Poll
        {
            Code = "ABCD",
            Question = "Test case insensitivity",
            ChoiceMode = ChoiceMode.Single
        };
        _context.Polls.Add(poll);
        await _context.SaveChangesAsync();

        var option = new Option { PollId = poll.Id, Text = "Option", DisplayOrder = 0 };
        _context.Options.Add(option);
        await _context.SaveChangesAsync();

        // Act
        var result1 = await _resultsService.GetPollResults("abcd");
        var result2 = await _resultsService.GetPollResults("ABCD");
        var result3 = await _resultsService.GetPollResults("AbCd");

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result3.Should().NotBeNull();
        result1.PollCode.Should().Be("ABCD");
        result2.PollCode.Should().Be("ABCD");
        result3.PollCode.Should().Be("ABCD");
    }

    [Fact]
    public async Task GetPollResults_IncludesClosedStatus()
    {
        // Arrange
        var openPoll = new Poll
        {
            Code = "OPEN",
            Question = "Open poll",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = false
        };
        var closedPoll = new Poll
        {
            Code = "SHUT",
            Question = "Closed poll",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = true,
            ClosedAt = DateTime.UtcNow
        };
        _context.Polls.AddRange(openPoll, closedPoll);
        await _context.SaveChangesAsync();

        var option1 = new Option { PollId = openPoll.Id, Text = "A", DisplayOrder = 0 };
        var option2 = new Option { PollId = closedPoll.Id, Text = "B", DisplayOrder = 0 };
        _context.Options.AddRange(option1, option2);
        await _context.SaveChangesAsync();

        // Act
        var openResult = await _resultsService.GetPollResults("OPEN");
        var closedResult = await _resultsService.GetPollResults("SHUT");

        // Assert
        openResult.IsClosed.Should().BeFalse();
        closedResult.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task GetPollResults_OrdersOptionsByDisplayOrder()
    {
        // Arrange
        var poll = new Poll
        {
            Code = "SORT",
            Question = "Test ordering",
            ChoiceMode = ChoiceMode.Single
        };
        _context.Polls.Add(poll);
        await _context.SaveChangesAsync();

        // Add options in non-sequential order
        var option3 = new Option { PollId = poll.Id, Text = "Third", DisplayOrder = 2 };
        var option1 = new Option { PollId = poll.Id, Text = "First", DisplayOrder = 0 };
        var option2 = new Option { PollId = poll.Id, Text = "Second", DisplayOrder = 1 };
        _context.Options.AddRange(option3, option1, option2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _resultsService.GetPollResults("SORT");

        // Assert
        result.Options.Should().HaveCount(3);
        result.Options[0].Text.Should().Be("First");
        result.Options[1].Text.Should().Be("Second");
        result.Options[2].Text.Should().Be("Third");
    }
}
