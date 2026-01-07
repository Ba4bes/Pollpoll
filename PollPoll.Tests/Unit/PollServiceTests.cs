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
    public async Task CreatePoll_ShouldNotAutoClosePreviousOpenPoll()
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

        // Assert - US5: Multiple active polls are allowed
        var previousPollInDb = await _context.Polls.FindAsync(previousPoll.Id);
        previousPollInDb.Should().NotBeNull();
        previousPollInDb!.IsClosed.Should().BeFalse("US5 allows multiple active polls");
        
        var newPollInDb = await _context.Polls.FindAsync(newPoll.Id);
        newPollInDb!.IsClosed.Should().BeFalse("new poll should be open");
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

    // ===== User Story 5: Multiple Active Polls and Manual Close =====

    [Fact]
    public async Task ClosePoll_ShouldSetIsClosedAndClosedAt()
    {
        // Arrange
        var poll = new Poll
        {
            Code = "OPEN",
            Question = "Open poll",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Polls.Add(poll);
        await _context.SaveChangesAsync();

        // Act
        await _sut.ClosePollAsync(poll.Code);

        // Assert
        var closedPoll = await _context.Polls.FirstOrDefaultAsync(p => p.Code == poll.Code);
        closedPoll.Should().NotBeNull();
        closedPoll!.IsClosed.Should().BeTrue();
        closedPoll.ClosedAt.Should().NotBeNull();
        closedPoll.ClosedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ClosePoll_ShouldReturnFalseForNonExistentPoll()
    {
        // Act
        var result = await _sut.ClosePollAsync("FAKE");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ClosePoll_ShouldReturnTrueForAlreadyClosedPoll()
    {
        // Arrange
        var poll = new Poll
        {
            Code = "CLSD",
            Question = "Closed poll",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = true,
            ClosedAt = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };
        _context.Polls.Add(poll);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.ClosePollAsync(poll.Code);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CreatePoll_ShouldAllowMultipleActivePolls()
    {
        // Arrange - Create first poll
        var poll1 = await _sut.CreatePollAsync("Poll 1", ChoiceMode.Single, new List<string> { "A", "B" });
        
        // Act - Create second poll (should NOT auto-close poll1 for US5)
        var poll2 = await _sut.CreatePollAsync("Poll 2", ChoiceMode.Single, new List<string> { "X", "Y" });

        // Assert
        var allPolls = await _context.Polls.ToListAsync();
        var openPolls = allPolls.Where(p => !p.IsClosed).ToList();
        
        // For US5, we want multiple active polls, so this test expects BOTH to be open
        // This will FAIL initially because current CreatePoll auto-closes previous polls
        openPolls.Should().HaveCount(2, "US5 allows multiple active polls");
        openPolls.Should().Contain(p => p.Id == poll1.Id);
        openPolls.Should().Contain(p => p.Id == poll2.Id);
    }

    [Fact]
    public async Task AutoCloseExpiredPolls_ShouldClosePolls7DaysOld()
    {
        // Arrange
        var expiredPoll = new Poll
        {
            Code = "OLD1",
            Question = "Expired poll",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = false,
            CreatedAt = DateTime.UtcNow.AddDays(-8) // 8 days old
        };
        var recentPoll = new Poll
        {
            Code = "NEW1",
            Question = "Recent poll",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = false,
            CreatedAt = DateTime.UtcNow.AddDays(-3) // 3 days old
        };
        _context.Polls.AddRange(expiredPoll, recentPoll);
        await _context.SaveChangesAsync();

        // Act
        var closedCount = await _sut.AutoCloseExpiredPollsAsync();

        // Assert
        closedCount.Should().Be(1, "only the 8-day old poll should be closed");
        
        var expiredPollInDb = await _context.Polls.FirstOrDefaultAsync(p => p.Code == "OLD1");
        expiredPollInDb!.IsClosed.Should().BeTrue();
        expiredPollInDb.ClosedAt.Should().NotBeNull();

        var recentPollInDb = await _context.Polls.FirstOrDefaultAsync(p => p.Code == "NEW1");
        recentPollInDb!.IsClosed.Should().BeFalse();
        recentPollInDb.ClosedAt.Should().BeNull();
    }

    [Fact]
    public async Task AutoCloseExpiredPolls_ShouldCloseExactly7DaysOld()
    {
        // Arrange
        var exactlyExpiredPoll = new Poll
        {
            Code = "EX7D",
            Question = "Exactly 7 days old",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = false,
            CreatedAt = DateTime.UtcNow.AddDays(-7).AddMinutes(-1) // Just over 7 days
        };
        _context.Polls.Add(exactlyExpiredPoll);
        await _context.SaveChangesAsync();

        // Act
        var closedCount = await _sut.AutoCloseExpiredPollsAsync();

        // Assert
        closedCount.Should().Be(1);
        var poll = await _context.Polls.FirstOrDefaultAsync(p => p.Code == "EX7D");
        poll!.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task AutoCloseExpiredPolls_ShouldNotCloseAlreadyClosedPolls()
    {
        // Arrange
        var alreadyClosed = new Poll
        {
            Code = "AC10",
            Question = "Already closed, 10 days old",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = true,
            ClosedAt = DateTime.UtcNow.AddDays(-5),
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        _context.Polls.Add(alreadyClosed);
        await _context.SaveChangesAsync();

        // Act
        var closedCount = await _sut.AutoCloseExpiredPollsAsync();

        // Assert
        closedCount.Should().Be(0, "already closed polls should not be counted");
    }

    [Fact]
    public async Task GetAllPolls_ShouldReturnAllPolls()
    {
        // Arrange
        var poll1 = new Poll { Code = "P001", Question = "Q1", ChoiceMode = ChoiceMode.Single, IsClosed = false, CreatedAt = DateTime.UtcNow };
        var poll2 = new Poll { Code = "P002", Question = "Q2", ChoiceMode = ChoiceMode.Single, IsClosed = true, ClosedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow.AddHours(-1) };
        _context.Polls.AddRange(poll1, poll2);
        await _context.SaveChangesAsync();

        // Act
        var polls = await _sut.GetAllPollsAsync();

        // Assert
        polls.Should().HaveCount(2);
        polls.Should().Contain(p => p.Code == "P001");
        polls.Should().Contain(p => p.Code == "P002");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
