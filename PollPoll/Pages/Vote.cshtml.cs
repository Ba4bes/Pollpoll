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
            ErrorMessage = "Poll not found. Check the code and try again.";
            return NotFound();
        }

        Options = Poll.Options!.OrderBy(o => o.DisplayOrder).ToList();

        // Check if poll is closed
        if (Poll.IsClosed)
        {
            ErrorMessage = "This poll is closed and no longer accepting votes.";
            return Page();
        }

        // Validate selection based on choice mode
        int[] selectedIds;

        if (Poll.ChoiceMode == ChoiceMode.Single)
        {
            if (SelectedOptionId == 0)
            {
                ErrorMessage = "Please select an option.";
                return Page();
            }
            selectedIds = new[] { SelectedOptionId };
        }
        else // Multi
        {
            if (SelectedOptionIds == null || !SelectedOptionIds.Any())
            {
                ErrorMessage = "Please select at least one option.";
                return Page();
            }
            selectedIds = SelectedOptionIds.ToArray();
        }

        // Validate that all selected options belong to this poll
        var validOptionIds = Options.Select(o => o.Id).ToHashSet();
        if (selectedIds.Any(id => !validOptionIds.Contains(id)))
        {
            ErrorMessage = "Invalid option selected.";
            return Page();
        }

        // Submit vote
        try
        {
            var voterId = _voteService.GetOrCreateVoterId();
            await _voteService.SubmitVoteAsync(Poll.Id, selectedIds, voterId);

            SuccessMessage = "Vote recorded!";
            return RedirectToPage("/Vote", new { code = Poll.Code });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"An error occurred while submitting your vote: {ex.Message}";
            return Page();
        }
    }
}
