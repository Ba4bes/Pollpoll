using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using PollPoll.Data;
using PollPoll.Models;
using PollPoll.Services;
using Xunit;

namespace PollPoll.Tests.Unit;

/// <summary>
/// Unit tests for VoteService
/// Tests cover: voter ID generation/retrieval, duplicate vote prevention, vote updates
/// </summary>
public class VoteServiceTests : IDisposable
{
    private readonly PollDbContext _context;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly VoteService _sut;
    private readonly DefaultHttpContext _httpContext;

    public VoteServiceTests()
    {
        // Use in-memory database for isolated unit testing
        var options = new DbContextOptionsBuilder<PollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new PollDbContext(options);
        
        // Mock HttpContextAccessor for cookie management
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _httpContext = new DefaultHttpContext();
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(_httpContext);

        _sut = new VoteService(_context, _httpContextAccessorMock.Object);
    }

    [Fact]
    public void GetOrCreateVoterId_ShouldGenerateNewVoterIdWhenCookieDoesNotExist()
    {
        // Arrange
        // No cookie exists

        // Act
        var voterId = _sut.GetOrCreateVoterId();

        // Assert
        voterId.Should().NotBeEmpty();
        Guid.TryParse(voterId.ToString(), out _).Should().BeTrue("VoterId should be a valid GUID");
        
        // Verify cookie was set
        var cookie = _httpContext.Response.Headers["Set-Cookie"].ToString();
        cookie.Should().Contain("VoterId=");
    }

    [Fact]
    public void GetOrCreateVoterId_ShouldReturnExistingVoterIdFromCookie()
    {
        // Arrange
        var existingVoterId = Guid.NewGuid();
        _httpContext.Request.Headers["Cookie"] = $"VoterId={existingVoterId}";

        // Act
        var voterId = _sut.GetOrCreateVoterId();

        // Assert
        voterId.Should().Be(existingVoterId);
    }

    [Fact]
    public async Task SubmitVote_ShouldCreateNewVoteWhenVoterHasNotVoted()
    {
        // Arrange
        var poll = await CreateTestPollAsync("Test poll", 3);
        var voterId = Guid.NewGuid();
        var option = await _context.Options.FirstAsync(o => o.PollId == poll.Id);

        // Act
        await _sut.SubmitVoteAsync(poll.Id, new[] { option.Id }, voterId);

        // Assert
        var vote = await _context.Votes.FirstOrDefaultAsync(v => v.VoterId == voterId);
        vote.Should().NotBeNull();
        vote!.PollId.Should().Be(poll.Id);
        vote.OptionId.Should().Be(option.Id);
        vote.SubmittedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SubmitVote_ShouldPreventDuplicateVotes_SingleChoice()
    {
        // Arrange
        var poll = await CreateTestPollAsync("Color poll", 3);
        var voterId = Guid.NewGuid();
        var options = await _context.Options.Where(o => o.PollId == poll.Id).ToListAsync();

        // First vote for option 0
        await _sut.SubmitVoteAsync(poll.Id, new[] { options[0].Id }, voterId);

        // Act - Change vote to option 1
        await _sut.SubmitVoteAsync(poll.Id, new[] { options[1].Id }, voterId);

        // Assert
        var votes = await _context.Votes.Where(v => v.VoterId == voterId).ToListAsync();
        votes.Should().HaveCount(1, "duplicate votes should be prevented");
        votes[0].OptionId.Should().Be(options[1].Id, "vote should be updated to new option");
    }

    [Fact]
    public async Task SubmitVote_ShouldDeleteOldVoteBeforeInsertingNew()
    {
        // Arrange
        var poll = await CreateTestPollAsync("Test", 2);
        var voterId = Guid.NewGuid();
        var options = await _context.Options.Where(o => o.PollId == poll.Id).ToListAsync();

        // Create initial vote
        var initialVote = new Vote
        {
            PollId = poll.Id,
            OptionId = options[0].Id,
            VoterId = voterId,
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        _context.Votes.Add(initialVote);
        await _context.SaveChangesAsync();

        // Act - Submit new vote
        await _sut.SubmitVoteAsync(poll.Id, new[] { options[1].Id }, voterId);

        // Assert
        var oldVote = await _context.Votes.FindAsync(initialVote.Id);
        oldVote.Should().BeNull("old vote should be deleted");

        var currentVotes = await _context.Votes.Where(v => v.VoterId == voterId).ToListAsync();
        currentVotes.Should().HaveCount(1);
        currentVotes[0].OptionId.Should().Be(options[1].Id);
    }

    [Fact]
    public async Task SubmitVote_ShouldSupportMultipleVotesForMultiChoicePolls()
    {
        // Arrange
        var poll = await CreateTestPollAsync("Multi-choice poll", 4, ChoiceMode.Multi);
        var voterId = Guid.NewGuid();
        var options = await _context.Options.Where(o => o.PollId == poll.Id).ToListAsync();
        var selectedOptionIds = new[] { options[0].Id, options[2].Id, options[3].Id };

        // Act
        await _sut.SubmitVoteAsync(poll.Id, selectedOptionIds, voterId);

        // Assert
        var votes = await _context.Votes
            .Where(v => v.VoterId == voterId && v.PollId == poll.Id)
            .ToListAsync();

        votes.Should().HaveCount(3);
        votes.Select(v => v.OptionId).Should().BeEquivalentTo(selectedOptionIds);
    }

    [Fact]
    public async Task SubmitVote_ShouldReplaceAllPreviousVotesInMultiChoice()
    {
        // Arrange
        var poll = await CreateTestPollAsync("Multi poll", 4, ChoiceMode.Multi);
        var voterId = Guid.NewGuid();
        var options = await _context.Options.Where(o => o.PollId == poll.Id).ToListAsync();

        // First vote: select options 0, 1
        await _sut.SubmitVoteAsync(poll.Id, new[] { options[0].Id, options[1].Id }, voterId);

        // Act - Change to options 2, 3
        await _sut.SubmitVoteAsync(poll.Id, new[] { options[2].Id, options[3].Id }, voterId);

        // Assert
        var votes = await _context.Votes.Where(v => v.VoterId == voterId).ToListAsync();
        votes.Should().HaveCount(2, "should only have new votes, old ones deleted");
        votes.Select(v => v.OptionId).Should().BeEquivalentTo(new[] { options[2].Id, options[3].Id });
    }

    [Fact]
    public async Task SubmitVote_ShouldUseTransactionToEnsureAtomicity()
    {
        // Arrange
        var poll = await CreateTestPollAsync("Test", 2);
        var voterId = Guid.NewGuid();
        var options = await _context.Options.Where(o => o.PollId == poll.Id).ToListAsync();

        // Create initial vote
        await _sut.SubmitVoteAsync(poll.Id, new[] { options[0].Id }, voterId);

        // Act - Update vote
        await _sut.SubmitVoteAsync(poll.Id, new[] { options[1].Id }, voterId);

        // Assert - No orphaned votes should exist
        var allVotes = await _context.Votes.Where(v => v.VoterId == voterId).ToListAsync();
        allVotes.Should().HaveCount(1, "transaction should ensure delete + insert is atomic");
    }

    private async Task<Poll> CreateTestPollAsync(string question, int optionCount, ChoiceMode choiceMode = ChoiceMode.Single)
    {
        var poll = new Poll
        {
            Code = Guid.NewGuid().ToString().Substring(0, 4).ToUpper(),
            Question = question,
            ChoiceMode = choiceMode,
            IsClosed = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Polls.Add(poll);
        await _context.SaveChangesAsync();

        for (int i = 0; i < optionCount; i++)
        {
            var option = new Option
            {
                PollId = poll.Id,
                Text = $"Option {i}",
                DisplayOrder = i
            };
            _context.Options.Add(option);
        }
        await _context.SaveChangesAsync();

        return poll;
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
