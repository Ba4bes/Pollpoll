using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PollPoll.Data;
using PollPoll.Models;
using PollPoll.Services;
using PollPoll.Filters;

namespace PollPoll.Pages;

public class IndexModel : AuthenticatedPageModel
{
    private readonly PollService _pollService;
    private readonly PollDbContext _context;

    public IndexModel(
        PollService pollService,
        PollDbContext context,
        ILogger<IndexModel> logger,
        IConfiguration configuration) : base(configuration, logger)
    {
        _pollService = pollService;
        _context = context;
    }

    [BindProperty]
    public string Question { get; set; } = string.Empty;

    [BindProperty]
    public ChoiceMode ChoiceMode { get; set; } = ChoiceMode.Single;

    [BindProperty]
    public List<string> Options { get; set; } = new() { "", "" };

    public List<Poll> Polls { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string HostToken => Configuration["HostAuth:Token"] ?? string.Empty;

    public async Task OnGetAsync()
    {
        // Load success message from TempData if present
        if (TempData["SuccessMessage"] != null)
        {
            SuccessMessage = TempData["SuccessMessage"]?.ToString();
        }

        // Load all non-archived polls ordered by status (open first) then date descending
        Polls = await _pollService.GetAllPollsAsync(isArchived: false);
        
        // Sort by IsClosed ASC (open polls first), then CreatedAt DESC (newest first)
        Polls = Polls.OrderBy(p => p.IsClosed).ThenByDescending(p => p.CreatedAt).ToList();
    }

    public async Task<IActionResult> OnPostCreatePollAsync()
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(Question))
            {
                ErrorMessage = "Question is required.";
                await OnGetAsync();
                return Page();
            }

            if (Question.Length > 500)
            {
                ErrorMessage = "Question must be 500 characters or less.";
                await OnGetAsync();
                return Page();
            }

            // Filter out empty options
            var validOptions = Options.Where(o => !string.IsNullOrWhiteSpace(o)).ToList();

            if (validOptions.Count < 2)
            {
                ErrorMessage = "At least 2 options are required.";
                await OnGetAsync();
                return Page();
            }

            if (validOptions.Count > 6)
            {
                ErrorMessage = "Maximum 6 options allowed.";
                await OnGetAsync();
                return Page();
            }

            if (validOptions.Any(o => o.Length > 200))
            {
                ErrorMessage = "Each option must be 200 characters or less.";
                await OnGetAsync();
                return Page();
            }

            // Create poll using PollService
            var poll = await _pollService.CreatePollAsync(Question, ChoiceMode, validOptions);

            SuccessMessage = $"Poll created successfully! Code: {poll.Code}";

            // Clear form
            Question = string.Empty;
            Options = new() { "", "" };

            await OnGetAsync();
            return Page();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating poll");
            ErrorMessage = "An error occurred while creating the poll. Please try again.";
            await OnGetAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostClosePollAsync(int pollId)
    {
        try
        {
            var poll = await _context.Polls.FindAsync(pollId);
            if (poll == null)
            {
                ErrorMessage = "Poll not found.";
                await OnGetAsync();
                return Page();
            }

            poll.IsClosed = true;
            poll.ClosedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Poll closed successfully.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error closing poll");
            ErrorMessage = "An error occurred while closing the poll. Please try again.";
            await OnGetAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostArchivePollAsync(string pollCode)
    {
        try
        {
            var success = await _pollService.ArchivePollAsync(pollCode);
            if (!success)
            {
                ErrorMessage = "Poll not found.";
                await OnGetAsync();
                return Page();
            }

            TempData["SuccessMessage"] = $"Poll {pollCode} archived successfully.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error archiving poll");
            ErrorMessage = "An error occurred while archiving the poll. Please try again.";
            await OnGetAsync();
            return Page();
        }
    }
}
