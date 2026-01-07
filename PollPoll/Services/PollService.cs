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
    /// Creates a new poll with a unique 4-character code, auto-closes previous open poll
    /// </summary>
    /// <param name="question">Poll question (max 500 characters)</param>
    /// <param name="choiceMode">Single or Multi choice mode</param>
    /// <param name="optionTexts">List of 2-6 option texts</param>
    /// <returns>Created poll with generated code</returns>
    public async Task<Poll> CreatePollAsync(string question, ChoiceMode choiceMode, List<string> optionTexts)
    {
        // Auto-close any previous open poll
        await AutoClosePreviousPollAsync();

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
}
