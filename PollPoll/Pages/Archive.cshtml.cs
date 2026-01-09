using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PollPoll.Models;
using PollPoll.Services;
using PollPoll.Filters;

namespace PollPoll.Pages;

public class ArchiveModel : AuthenticatedPageModel
{
    private readonly PollService _pollService;

    public ArchiveModel(
        PollService pollService,
        ILogger<ArchiveModel> logger,
        IConfiguration configuration) : base(configuration, logger)
    {
        _pollService = pollService;
    }

    public List<Poll> ArchivedPolls { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        // Load success message from TempData if present
        if (TempData["SuccessMessage"] != null)
        {
            SuccessMessage = TempData["SuccessMessage"]?.ToString();
        }

        // Load all archived polls
        ArchivedPolls = await _pollService.GetAllPollsAsync(isArchived: true);
    }

    public async Task<IActionResult> OnPostUnarchivePollAsync(string pollCode)
    {
        try
        {
            var success = await _pollService.UnarchivePollAsync(pollCode);
            if (!success)
            {
                ErrorMessage = "Poll not found.";
                await OnGetAsync();
                return Page();
            }

            TempData["SuccessMessage"] = $"Poll {pollCode} restored successfully.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error unarchiving poll");
            ErrorMessage = "An error occurred while unarchiving the poll. Please try again.";
            await OnGetAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeletePollAsync(string pollCode)
    {
        try
        {
            var success = await _pollService.DeletePollAsync(pollCode);
            if (!success)
            {
                ErrorMessage = "Poll not found.";
                await OnGetAsync();
                return Page();
            }

            TempData["SuccessMessage"] = $"Poll {pollCode} deleted permanently.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting poll");
            ErrorMessage = "An error occurred while deleting the poll. Please try again.";
            await OnGetAsync();
            return Page();
        }
    }
}
