using Microsoft.EntityFrameworkCore;
using PollPoll.Data;
using PollPoll.Models;

namespace PollPoll.Services;

/// <summary>
/// Service for managing polls: creation, code generation, and lifecycle management
/// </summary>
public class PollService
{
    private readonly PollDbContext _context;
    private readonly Random _random = new();

    public PollService(PollDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a new poll with a unique 4-character code
    /// For US5: Does NOT auto-close previous polls (allows multiple active polls)
    /// </summary>
    /// <param name="question">Poll question (max 500 characters)</param>
    /// <param name="choiceMode">Single or Multi choice mode</param>
    /// <param name="optionTexts">List of 2-6 option texts</param>
    /// <returns>Created poll with generated code</returns>
    public async Task<Poll> CreatePollAsync(string question, ChoiceMode choiceMode, List<string> optionTexts)
    {
        // US5: REMOVED auto-close logic to allow multiple active polls
        // await AutoClosePreviousPollAsync();

        // Generate unique 4-character code
        var code = await GenerateUniquePollCodeAsync();

        // Create poll
        var poll = new Poll
        {
            Code = code,
            Question = question,
            ChoiceMode = choiceMode,
            IsClosed = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Polls.Add(poll);
        await _context.SaveChangesAsync();

        // Create options
        for (int i = 0; i < optionTexts.Count; i++)
        {
            var option = new Option
            {
                PollId = poll.Id,
                Text = optionTexts[i],
                DisplayOrder = i
            };
            _context.Options.Add(option);
        }

        await _context.SaveChangesAsync();

        return poll;
    }

    /// <summary>
    /// Gets a poll by its code (case-insensitive)
    /// </summary>
    /// <param name="code">Poll code</param>
    /// <returns>Poll or null if not found</returns>
    public async Task<Poll?> GetPollByCodeAsync(string code)
    {
        var upperCode = code.ToUpper();
        return await _context.Polls
            .Include(p => p.Options)
            .Where(p => p.Code == upperCode)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Generates a unique 4-character alphanumeric code (uppercase)
    /// Loops until a unique code is found
    /// </summary>
    private async Task<string> GenerateUniquePollCodeAsync()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code;
        bool isUnique;

        do
        {
            // Generate 4-character code
            code = new string(Enumerable.Range(0, 4)
                .Select(_ => chars[_random.Next(chars.Length)])
                .ToArray());

            // Check uniqueness
            isUnique = !await _context.Polls.AnyAsync(p => p.Code == code);
        }
        while (!isUnique);

        return code;
    }

    /// <summary>
    /// Auto-closes any currently open poll
    /// NOTE: For US5, this method is no longer called in CreatePollAsync
    /// Kept for backwards compatibility or manual use
    /// </summary>
    private async Task AutoClosePreviousPollAsync()
    {
        var openPoll = await _context.Polls
            .FirstOrDefaultAsync(p => !p.IsClosed);

        if (openPoll != null)
        {
            openPoll.IsClosed = true;
            openPoll.ClosedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// US5: Manually closes a poll by code
    /// </summary>
    /// <param name="code">Poll code to close</param>
    /// <returns>True if poll was found and closed, false if not found</returns>
    public async Task<bool> ClosePollAsync(string code)
    {
        var upperCode = code.ToUpper();
        var poll = await _context.Polls.FirstOrDefaultAsync(p => p.Code == upperCode);

        if (poll == null)
        {
            return false;
        }

        if (!poll.IsClosed)
        {
            poll.IsClosed = true;
            poll.ClosedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return true;
    }

    /// <summary>
    /// US5: Auto-closes polls that are 7+ days old
    /// Should be called by a background service/scheduled task
    /// </summary>
    /// <returns>Number of polls closed</returns>
    public async Task<int> AutoCloseExpiredPollsAsync()
    {
        var expirationDate = DateTime.UtcNow.AddDays(-7);
        
        var expiredPolls = await _context.Polls
            .Where(p => !p.IsClosed && p.CreatedAt < expirationDate)
            .ToListAsync();

        foreach (var poll in expiredPolls)
        {
            poll.IsClosed = true;
            poll.ClosedAt = DateTime.UtcNow;
        }

        if (expiredPolls.Any())
        {
            await _context.SaveChangesAsync();
        }

        return expiredPolls.Count;
    }

    /// <summary>
    /// US5: Gets all polls (for host dashboard)
    /// </summary>
    /// <returns>List of all polls, ordered by creation date descending</returns>
    public async Task<List<Poll>> GetAllPollsAsync()
    {
        return await _context.Polls
            .Include(p => p.Options)
            .Include(p => p.Votes)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }
}
