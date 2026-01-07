using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PollPoll.Data;
using PollPoll.Models;
using PollPoll.Services;

namespace PollPoll.Pages;

/// <summary>
/// Page model for viewing and voting on polls
/// GET /p/{code} - Display poll
/// POST /p/{code} - Submit vote
/// </summary>
public class VoteModel : PageModel
{
    private readonly PollDbContext _context;
    private readonly VoteService _voteService;

    public VoteModel(PollDbContext context, VoteService voteService)
    {
        _context = context;
        _voteService = voteService;
    }

    public Poll? Poll { get; set; }
    public List<Option> Options { get; set; } = new();
    public bool HasVoted { get; set; }
    public List<int>? PreviousVoteOptionIds { get; set; }
    
    [TempData]
    public string? ErrorMessage { get; set; }
    
    [TempData]
    public string? SuccessMessage { get; set; }

    [BindProperty]
    public int SelectedOptionId { get; set; }

    [BindProperty]
    public List<int> SelectedOptionIds { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string code)
    {
        // Load poll with options
        Poll = await _context.Polls
            .Include(p => p.Options)
            .FirstOrDefaultAsync(p => p.Code == code.ToUpper());

        if (Poll == null)
        {
            return NotFound();
        }

        Options = Poll.Options!.OrderBy(o => o.DisplayOrder).ToList();

        // Check if user has already voted
        var voterId = _voteService.GetOrCreateVoterId();
        var existingVotes = await _context.Votes
            .Where(v => v.PollId == Poll.Id && v.VoterId == voterId)
            .ToListAsync();

        if (existingVotes.Any())
        {
            HasVoted = true;
            PreviousVoteOptionIds = existingVotes.Select(v => v.OptionId).ToList();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string code)
    {
        // Load poll
        Poll = await _context.Polls
            .Include(p => p.Options)
            .FirstOrDefaultAsync(p => p.Code == code.ToUpper());

        if (Poll == null)
        {
            ErrorMessage = "Poll not found. Please check the poll code and try again. If you scanned a QR code, try refreshing the page or re-scanning.";
            return NotFound();
        }

        Options = Poll.Options!.OrderBy(o => o.DisplayOrder).ToList();

        // Check if poll is closed
        if (Poll.IsClosed)
        {
            var closedMessage = Poll.ClosedAt.HasValue 
                ? $"This poll was closed on {Poll.ClosedAt.Value:yyyy-MM-dd HH:mm} UTC and is no longer accepting votes."
                : "This poll is closed and no longer accepting votes.";
            ErrorMessage = $"{closedMessage} You can still view the results.";
            return Page();
        }

        // Validate selection based on choice mode
        int[] selectedIds;

        if (Poll.ChoiceMode == ChoiceMode.Single)
        {
            if (SelectedOptionId == 0)
            {
                ErrorMessage = "Please select one option before submitting your vote.";
                return Page();
            }
            selectedIds = new[] { SelectedOptionId };
        }
        else // Multi
        {
            if (SelectedOptionIds == null || !SelectedOptionIds.Any())
            {
                ErrorMessage = "Please select at least one option before submitting. This poll allows multiple selections.";
                return Page();
            }
            selectedIds = SelectedOptionIds.ToArray();
        }

        // Validate that all selected options belong to this poll
        var validOptionIds = Options.Select(o => o.Id).ToHashSet();
        if (selectedIds.Any(id => !validOptionIds.Contains(id)))
        {
            ErrorMessage = "One or more selected options are invalid. Please refresh the page and try again.";
            return Page();
        }

        // Submit vote
        try
        {
            var voterId = _voteService.GetOrCreateVoterId();
            await _voteService.SubmitVoteAsync(Poll.Id, selectedIds, voterId);

            SuccessMessage = HasVoted ? "Your vote has been updated successfully!" : "Vote recorded successfully!";
            return RedirectToPage("/Vote", new { code = Poll.Code });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"We couldn't submit your vote right now. Please try again in a moment. If the problem persists, contact the poll host. (Error: {ex.Message})";
            return Page();
        }
    }
}
