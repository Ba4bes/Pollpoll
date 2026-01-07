using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PollPoll.Data;
using PollPoll.Models;
using PollPoll.Services;
using Xunit;

namespace PollPoll.Tests.Unit;

/// <summary>
/// Unit tests for PollService
/// Tests cover: code generation uniqueness, auto-close previous poll, poll creation
/// </summary>
public class PollServiceTests : IDisposable
{
    private readonly PollDbContext _context;
    private readonly PollService _sut;

    public PollServiceTests()
    {
        // Use in-memory database for isolated unit testing
        var options = new DbContextOptionsBuilder<PollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new PollDbContext(options);
        _sut = new PollService(_context);
    }

    [Fact]
    public async Task CreatePoll_ShouldGenerateUnique4CharacterCode()
    {
        // Arrange
        var question = "What's your favorite color?";
        var options = new List<string> { "Red", "Blue", "Green" };

        // Act
        var result = await _sut.CreatePollAsync(question, ChoiceMode.Single, options);

        // Assert
        result.Code.Should().NotBeNullOrEmpty();
        result.Code.Should().HaveLength(4);
        result.Code.Should().MatchRegex("^[A-Z0-9]{4}$", "code should be 4 uppercase alphanumeric characters");
    }

    [Fact]
    public async Task CreatePoll_ShouldGenerateUniqueCodeWhenCollisionOccurs()
    {
        // Arrange
        var existingPoll = new Poll
        {
            Code = "TEST",
            Question = "Existing poll",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Polls.Add(existingPoll);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.CreatePollAsync("New poll", ChoiceMode.Single, new List<string> { "Option 1", "Option 2" });

        // Assert
        result.Code.Should().NotBe("TEST", "code should be unique");
        var pollsInDb = await _context.Polls.ToListAsync();
        pollsInDb.Select(p => p.Code).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task CreatePoll_ShouldAutoClosePreviousOpenPoll()
    {
        // Arrange
        var previousPoll = new Poll
        {
            Code = "PREV",
            Question = "Previous poll",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Polls.Add(previousPoll);
        await _context.SaveChangesAsync();

        // Act
        var newPoll = await _sut.CreatePollAsync("New poll", ChoiceMode.Single, new List<string> { "A", "B" });

        // Assert
        var previousPollInDb = await _context.Polls.FindAsync(previousPoll.Id);
        previousPollInDb.Should().NotBeNull();
        previousPollInDb!.IsClosed.Should().BeTrue("previous poll should be auto-closed");
        previousPollInDb.ClosedAt.Should().NotBeNull("ClosedAt should be set");
        previousPollInDb.ClosedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreatePoll_ShouldNotAutoCloseAlreadyClosedPoll()
    {
        // Arrange
        var closedAt = DateTime.UtcNow.AddHours(-1);
        var closedPoll = new Poll
        {
            Code = "CLSD",
            Question = "Closed poll",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = true,
            ClosedAt = closedAt,
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };
        _context.Polls.Add(closedPoll);
        await _context.SaveChangesAsync();

        // Act
        await _sut.CreatePollAsync("New poll", ChoiceMode.Single, new List<string> { "X", "Y" });

        // Assert
        var closedPollInDb = await _context.Polls.FindAsync(closedPoll.Id);
        closedPollInDb!.ClosedAt.Should().Be(closedAt, "already closed poll's ClosedAt should not change");
    }

    [Fact]
    public async Task CreatePoll_ShouldSavePollWithAllProperties()
    {
        // Arrange
        var question = "Best programming language?";
        var choiceMode = ChoiceMode.Multi;
        var options = new List<string> { "C#", "Python", "JavaScript", "Go" };

        // Act
        var result = await _sut.CreatePollAsync(question, choiceMode, options);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Question.Should().Be(question);
        result.ChoiceMode.Should().Be(choiceMode);
        result.IsClosed.Should().BeFalse("new poll should be open");
        result.ClosedAt.Should().BeNull("new poll should not have ClosedAt");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreatePoll_ShouldCreateOptionsWithCorrectDisplayOrder()
    {
        // Arrange
        var options = new List<string> { "First", "Second", "Third" };

        // Act
        var poll = await _sut.CreatePollAsync("Test question", ChoiceMode.Single, options);

        // Assert
        var savedOptions = await _context.Options
            .Where(o => o.PollId == poll.Id)
            .OrderBy(o => o.DisplayOrder)
            .ToListAsync();

        savedOptions.Should().HaveCount(3);
        savedOptions[0].Text.Should().Be("First");
        savedOptions[0].DisplayOrder.Should().Be(0);
        savedOptions[1].Text.Should().Be("Second");
        savedOptions[1].DisplayOrder.Should().Be(1);
        savedOptions[2].Text.Should().Be("Third");
        savedOptions[2].DisplayOrder.Should().Be(2);
    }

    [Theory]
    [InlineData(2)] // Minimum options
    [InlineData(6)] // Maximum options
    public async Task CreatePoll_ShouldAcceptValidNumberOfOptions(int optionCount)
    {
        // Arrange
        var options = Enumerable.Range(1, optionCount).Select(i => $"Option {i}").ToList();

        // Act
        var poll = await _sut.CreatePollAsync("Test", ChoiceMode.Single, options);

        // Assert
        var savedOptions = await _context.Options.Where(o => o.PollId == poll.Id).ToListAsync();
        savedOptions.Should().HaveCount(optionCount);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
