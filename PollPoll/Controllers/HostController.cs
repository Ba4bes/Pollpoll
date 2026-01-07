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
            return BadRequest(new { error = "Validation failed", details = new { question = new[] { "Question is required" } } });
        }

        if (request.Question.Length > 500)
        {
            return BadRequest(new { error = "Validation failed", details = new { question = new[] { "Question must be 500 characters or less" } } });
        }

        if (request.Options == null || request.Options.Length < 2 || request.Options.Length > 6)
        {
            return BadRequest(new { error = "Validation failed", details = new { options = new[] { "Must provide 2-6 options" } } });
        }

        foreach (var option in request.Options)
        {
            if (string.IsNullOrWhiteSpace(option.Text))
            {
                return BadRequest(new { error = "Validation failed", details = new { options = new[] { "All options must have text" } } });
            }

            if (option.Text.Length > 200)
            {
                return BadRequest(new { error = "Validation failed", details = new { options = new[] { "Option text must be 200 characters or less" } } });
            }
        }

        if (!Enum.TryParse<ChoiceMode>(request.ChoiceMode, true, out var choiceMode))
        {
            return BadRequest(new { error = "Validation failed", details = new { choiceMode = new[] { "ChoiceMode must be 'Single' or 'Multi'" } } });
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
            return NotFound(new { error = "Poll not found" });
        }

        // Check if poll exists
        var poll = await _pollService.GetPollByCodeAsync(code);
        if (poll == null)
        {
            return NotFound(new { error = "Poll not found" });
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
    /// Request model for creating a poll
    /// </summary>
    public record CreatePollRequest(
        [Required] string Question,
        [Required] string ChoiceMode,
        [Required] OptionRequest[] Options
    );

    public record OptionRequest([Required] string Text);
}
