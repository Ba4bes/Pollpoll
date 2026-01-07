using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PollPoll.Data;
using PollPoll.Models;
using PollPoll.Hubs;

namespace PollPoll.Services;

/// <summary>
/// Service for managing votes: submission, duplicate prevention, voter tracking
/// </summary>
public class VoteService
{
    private readonly PollDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHubContext<ResultsHub> _hubContext;
    private const string VoterIdCookieName = "VoterId";

    public VoteService(PollDbContext context, IHttpContextAccessor httpContextAccessor, IHubContext<ResultsHub> hubContext)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Gets voter ID from cookie or creates a new one
    /// </summary>
    /// <returns>Voter ID as GUID</returns>
    public Guid GetOrCreateVoterId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            throw new InvalidOperationException("HttpContext is not available");
        }

        // Try to get existing voter ID from cookie
        if (httpContext.Request.Cookies.TryGetValue(VoterIdCookieName, out var voterIdString))
        {
            if (Guid.TryParse(voterIdString, out var existingVoterId))
            {
                return existingVoterId;
            }
        }

        // Generate new voter ID
        var newVoterId = Guid.NewGuid();

        // Set cookie (expires in 1 year)
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps, // Only secure over HTTPS
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        };

        httpContext.Response.Cookies.Append(VoterIdCookieName, newVoterId.ToString(), cookieOptions);

        return newVoterId;
    }

    /// <summary>
    /// Submits or updates vote(s) for a poll
    /// Prevents duplicates by deleting existing votes before inserting new ones
    /// Broadcasts vote update to ResultsHub after successful save
    /// </summary>
    /// <param name="pollId">Poll ID</param>
    /// <param name="selectedOptionIds">Array of selected option IDs</param>
    /// <param name="voterId">Voter's unique ID</param>
    public async Task SubmitVoteAsync(int pollId, int[] selectedOptionIds, Guid voterId)
    {
        // Use transaction to ensure atomic delete + insert
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Delete any existing votes by this voter for this poll
            var existingVotes = await _context.Votes
                .Where(v => v.PollId == pollId && v.VoterId == voterId)
                .ToListAsync();

            if (existingVotes.Any())
            {
                _context.Votes.RemoveRange(existingVotes);
                await _context.SaveChangesAsync();
            }

            // Insert new vote(s)
            foreach (var optionId in selectedOptionIds)
            {
                var vote = new Vote
                {
                    PollId = pollId,
                    OptionId = optionId,
                    VoterId = voterId,
                    SubmittedAt = DateTime.UtcNow
                };
                _context.Votes.Add(vote);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Get poll code for SignalR broadcast
            var poll = await _context.Polls.FindAsync(pollId);
            if (poll != null)
            {
                // Broadcast vote update to all clients watching this poll's results
                await _hubContext.Clients.Group(poll.Code).SendAsync("VoteUpdated", poll.Code);
            }
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
