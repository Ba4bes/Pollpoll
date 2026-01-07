using Microsoft.AspNetCore.Mvc;
using PollPoll.Models;
using PollPoll.Services;
using System.ComponentModel.DataAnnotations;

namespace PollPoll.Controllers;

/// <summary>
/// Controller for host operations (authenticated endpoints)
/// Handles poll creation and management
/// </summary>
[ApiController]
[Route("host")]
public class HostController : ControllerBase
{
    private readonly PollService _pollService;
    private readonly QRCodeService _qrCodeService;

    public HostController(PollService pollService, QRCodeService qrCodeService)
    {
        _pollService = pollService;
        _qrCodeService = qrCodeService;
    }

    /// <summary>
    /// POST /host/polls - Create a new poll
    /// </summary>
    [HttpPost("polls")]
    public async Task<IActionResult> CreatePoll([FromBody] CreatePollRequest request)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { 
                error = "Validation failed", 
                message = "Please provide a poll question.",
                details = new { question = new[] { "Question is required and cannot be empty" } } 
            });
        }

        if (request.Question.Length > 500)
        {
            return BadRequest(new { 
                error = "Validation failed", 
                message = $"Your question is too long ({request.Question.Length}/500 characters).",
                details = new { question = new[] { "Question must be 500 characters or less" } } 
            });
        }

        if (request.Options == null || request.Options.Length < 2 || request.Options.Length > 6)
        {
            var optionCount = request.Options?.Length ?? 0;
            var message = optionCount < 2 
                ? $"Please add more options. You provided {optionCount}, but polls require 2-6 options."
                : $"Too many options. You provided {optionCount}, but the maximum is 6 options.";
            return BadRequest(new { 
                error = "Validation failed", 
                message,
                details = new { options = new[] { "Must provide 2-6 options" } } 
            });
        }

        for (var i = 0; i < request.Options.Length; i++)
        {
            var option = request.Options[i];
            if (string.IsNullOrWhiteSpace(option.Text))
            {
                return BadRequest(new { 
                    error = "Validation failed", 
                    message = $"Option #{i + 1} is empty. All poll options must have text.",
                    details = new { options = new[] { $"Option #{i + 1} is required" } } 
                });
            }

            if (option.Text.Length > 200)
            {
                return BadRequest(new { 
                    error = "Validation failed", 
                    message = $"Option #{i + 1} is too long ({option.Text.Length}/200 characters).",
                    details = new { options = new[] { $"Option #{i + 1} must be 200 characters or less" } } 
                });
            }
        }

        if (!Enum.TryParse<ChoiceMode>(request.ChoiceMode, true, out var choiceMode))
        {
            return BadRequest(new { 
                error = "Validation failed", 
                message = $"Invalid choice mode '{request.ChoiceMode}'. Please use 'Single' or 'Multi'.",
                details = new { choiceMode = new[] { "ChoiceMode must be 'Single' or 'Multi'" } } 
            });
        }

        // Create poll
        var poll = await _pollService.CreatePollAsync(
            request.Question,
            choiceMode,
            request.Options.Select(o => o.Text).ToList()
        );

        // Generate URLs
        var joinUrl = $"/p/{poll.Code}";
        var absoluteJoinUrl = $"{Request.Scheme}://{Request.Host}{joinUrl}";

        // Generate QR code
        var qrCodeDataUrl = _qrCodeService.GenerateQRCode(poll.Code);

        // Return response
        var response = new
        {
            pollId = poll.Id,
            code = poll.Code,
            question = poll.Question,
            choiceMode = poll.ChoiceMode.ToString(),
            joinUrl,
            absoluteJoinUrl,
            qrCodeDataUrl,
            createdAt = poll.CreatedAt
        };

        return StatusCode(201, response);
    }

    /// <summary>
    /// GET /host/polls/{code}/qr - Get QR code for an existing poll
    /// </summary>
    [HttpGet("polls/{code}/qr")]
    public async Task<IActionResult> GetQRCode(string code)
    {
        // Validate poll code format (4 uppercase alphanumeric characters based on PollService implementation)
        if (string.IsNullOrWhiteSpace(code) || code.Length != 4 || !code.All(char.IsLetterOrDigit) || !code.All(char.IsUpper))
        {
            return NotFound(new { 
                error = "Poll not found", 
                message = $"Invalid poll code format: '{code}'. Poll codes are 4 uppercase letters/numbers (e.g., 'A7X9')."
            });
        }

        // Check if poll exists
        var poll = await _pollService.GetPollByCodeAsync(code);
        if (poll == null)
        {
            return NotFound(new { 
                error = "Poll not found", 
                message = $"No poll found with code '{code}'. Please verify the code and try again."
            });
        }

        // Generate QR code
        var absoluteJoinUrl = $"{Request.Scheme}://{Request.Host}/p/{code}";
        var qrCodeDataUrl = _qrCodeService.GenerateQRCode(code);

        // Return response
        var response = new
        {
            pollCode = code,
            qrCodeDataUrl,
            absoluteUrl = absoluteJoinUrl
        };

        return Ok(response);
    }

    /// <summary>
    /// GET /host/polls - List all polls (for host dashboard)
    /// US5: Returns all polls, both open and closed
    /// </summary>
    [HttpGet("polls")]
    public async Task<IActionResult> GetAllPolls()
    {
        var polls = await _pollService.GetAllPollsAsync();

        var pollResponses = polls.Select(p => new
        {
            pollId = p.Id,
            code = p.Code,
            question = p.Question,
            isClosed = p.IsClosed,
            totalVotes = p.Votes?.Count ?? 0,
            createdAt = p.CreatedAt,
            closedAt = p.ClosedAt,
            resultsUrl = $"/p/{p.Code}/results"
        });

        return Ok(new { polls = pollResponses });
    }

    /// <summary>
    /// POST /host/polls/{code}/close - Manually close a poll
    /// US5: Allows host to manually close a poll before 7-day auto-timeout
    /// </summary>
    [HttpPost("polls/{code}/close")]
    public async Task<IActionResult> ClosePoll(string code)
    {
        var result = await _pollService.ClosePollAsync(code);

        if (!result)
        {
            return NotFound(new { error = "Poll not found" });
        }

        return Ok(new { message = "Poll closed successfully", code });
    }

    /// <summary>
    /// Request model for creating a poll
    /// </summary>
    public record CreatePollRequest(
        [Required] string Question,
        [Required] string ChoiceMode,
        [Required] OptionRequest[] Options
    );

    public record OptionRequest([Required] string Text);
}
