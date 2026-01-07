using Microsoft.AspNetCore.Mvc;
using PollPoll.Services;

namespace PollPoll.Controllers;

/// <summary>
/// API controller for poll results (JSON endpoints)
/// </summary>
[ApiController]
[Route("api/results")]
public class ResultsController : ControllerBase
{
    private readonly ResultsService _resultsService;

    public ResultsController(ResultsService resultsService)
    {
        _resultsService = resultsService;
    }

    /// <summary>
    /// Get poll results as JSON
    /// </summary>
    /// <param name="code">Poll code (case-insensitive)</param>
    /// <returns>Poll results with vote counts and percentages</returns>
    [HttpGet("{code}")]
    public async Task<IActionResult> GetResults(string code)
    {
        var results = await _resultsService.GetPollResults(code);

        if (results == null)
        {
            return NotFound();
        }

        return Ok(results);
    }
}
