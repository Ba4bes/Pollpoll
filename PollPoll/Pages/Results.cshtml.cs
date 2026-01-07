using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PollPoll.Services;
using PollPoll.Models;

namespace PollPoll.Pages;

public class ResultsModel : PageModel
{
    private readonly ResultsService _resultsService;
    private readonly ILogger<ResultsModel> _logger;

    public ResultsModel(ResultsService resultsService, ILogger<ResultsModel> logger)
    {
        _resultsService = resultsService;
        _logger = logger;
    }

    public PollResults? Results { get; private set; }
    public string PollCode { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            _logger.LogWarning("Results requested with empty poll code");
            return NotFound("Poll code is required. Please provide a valid 4-character poll code (e.g., /p/A7X9/results).");
        }

        PollCode = code.ToUpper();
        Results = await _resultsService.GetPollResults(PollCode);

        if (Results == null)
        {
            _logger.LogWarning("Poll not found for code: {Code}", PollCode);
            return NotFound($"Poll '{PollCode}' not found. Please check the poll code and try again. Make sure the code is correct and the poll hasn't been deleted.");
        }

        return Page();
    }
}
