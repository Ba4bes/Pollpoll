using Microsoft.EntityFrameworkCore;
using PollPoll.Data;
using PollPoll.Models;

namespace PollPoll.Services;

/// <summary>
/// Service for retrieving poll results with vote counts and percentages
/// </summary>
public class ResultsService
{
    private readonly PollDbContext _context;

    public ResultsService(PollDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get poll results with vote counts and percentages for all options
    /// </summary>
    /// <param name="code">Poll code (case-insensitive)</param>
    /// <returns>Poll results or null if poll not found</returns>
    public async Task<PollResults?> GetPollResults(string code)
    {
        var upperCode = code.ToUpper();

        var poll = await _context.Polls
            .Include(p => p.Options)
            .Where(p => p.Code == upperCode)
            .FirstOrDefaultAsync();

        if (poll == null)
        {
            return null;
        }

        // Get vote counts for all options
        var voteCounts = await _context.Votes
            .Where(v => v.PollId == poll.Id)
            .GroupBy(v => v.OptionId)
            .Select(g => new { OptionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OptionId, x => x.Count);

        var totalVotes = voteCounts.Values.Sum();

        // Order options by DisplayOrder and map to result
        var optionResults = poll.Options
            .OrderBy(o => o.DisplayOrder)
            .Select(option =>
            {
                var voteCount = voteCounts.GetValueOrDefault(option.Id, 0);
                var percentage = totalVotes > 0 ? (voteCount * 100.0 / totalVotes) : 0.0;

                return new OptionResult
                {
                    OptionId = option.Id,
                    Text = option.Text,
                    VoteCount = voteCount,
                    Percentage = percentage
                };
            }).ToList();

        return new PollResults
        {
            PollCode = poll.Code,
            Question = poll.Question,
            IsClosed = poll.IsClosed,
            ClosedAt = poll.ClosedAt,
            TotalVotes = totalVotes,
            Options = optionResults
        };
    }
}
