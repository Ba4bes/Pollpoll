using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PollPoll.Data;
using PollPoll.Models;
using PollPoll.Services;

namespace PollPoll.Pages;

public class IndexModel : PageModel
{
    private readonly PollService _pollService;
    private readonly PollDbContext _context;
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;

    public IndexModel(
        PollService pollService, 
        PollDbContext context, 
        ILogger<IndexModel> logger,
        IConfiguration configuration)
    {
        _pollService = pollService;
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    [BindProperty]
    public string Question { get; set; } = string.Empty;

    [BindProperty]
    public List<string> Options { get; set; } = new() { "", "" };

    public Poll? CurrentPoll { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string HostToken => _configuration["HostAuth:Token"] ?? string.Empty;

    public async Task OnGetAsync()
    {
        // Load the current open poll if exists
        CurrentPoll = await _context.Polls
            .Include(p => p.Options.OrderBy(o => o.DisplayOrder))
            .Where(p => !p.IsClosed)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
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

            // Create poll using PollService (default to Single choice mode)
            var poll = await _pollService.CreatePollAsync(Question, ChoiceMode.Single, validOptions);

            SuccessMessage = $"Poll created successfully! Code: {poll.Code}";
            
            // Clear form
            Question = string.Empty;
            Options = new() { "", "" };

            await OnGetAsync();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating poll");
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

            SuccessMessage = "Poll closed successfully.";
            await OnGetAsync();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing poll");
            ErrorMessage = "An error occurred while closing the poll. Please try again.";
            await OnGetAsync();
            return Page();
        }
    }
}
