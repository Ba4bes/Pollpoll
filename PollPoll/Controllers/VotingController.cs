using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PollPoll.Data;
using PollPoll.Services;
using System.ComponentModel.DataAnnotations;

namespace PollPoll.Controllers;

/// <summary>
/// API controller for voting operations (participant endpoints)
/// </summary>
[ApiController]
[Route("p")]
public class VotingController : ControllerBase
{
    private readonly PollDbContext _context;
    private readonly VoteService _voteService;

    public VotingController(PollDbContext context, VoteService voteService)
    {
        _context = context;
        _voteService = voteService;
    }

    /// <summary>
    /// POST /p/{code}/vote - Submit vote
    /// </summary>
    [HttpPost("{code}/vote")]
    public async Task<IActionResult> SubmitVote(string code, [FromBody] VoteRequest request)
    {
        // Load poll
        var poll = await _context.Polls
            .Include(p => p.Options)
            .FirstOrDefaultAsync(p => p.Code == code.ToUpper());

        if (poll == null)
        {
            return NotFound(new { error = "Poll not found", message = "Check the code and try again." });
        }

        // Check if poll is closed
        if (poll.IsClosed)
        {
            return BadRequest(new { error = "Poll closed", message = "This poll is no longer accepting votes." });
        }

        // Validate selection
        if (request.SelectedOptionIds == null || !request.SelectedOptionIds.Any())
        {
            return BadRequest(new { error = "Validation failed", message = "Please select at least one option." });
        }

        // Validate based on choice mode
        if (poll.ChoiceMode == Models.ChoiceMode.Single && request.SelectedOptionIds.Length > 1)
        {
            return BadRequest(new { error = "Validation failed", message = "Single-choice polls allow only one selection." });
        }

        // Validate that all selected options belong to this poll
        var validOptionIds = poll.Options!.Select(o => o.Id).ToHashSet();
        if (request.SelectedOptionIds.Any(id => !validOptionIds.Contains(id)))
        {
            return BadRequest(new { error = "Validation failed", message = "Invalid option selected." });
        }

        // Submit vote
        try
        {
            var voterId = _voteService.GetOrCreateVoterId();
            await _voteService.SubmitVoteAsync(poll.Id, request.SelectedOptionIds, voterId);

            return Ok(new 
            { 
                success = true, 
                message = "Vote recorded!", 
                voterId = voterId 
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Server error", message = ex.Message });
        }
    }

    public record VoteRequest([Required] int[] SelectedOptionIds);
}
