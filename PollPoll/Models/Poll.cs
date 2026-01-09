using System.ComponentModel.DataAnnotations;

namespace PollPoll.Models;

/// <summary>
/// Represents a conference poll with a unique join code, question, and options.
/// </summary>
public class Poll
{
    /// <summary>
    /// Auto-increment primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 4-character alphanumeric join code (e.g., "K7F3").
    /// Unique and indexed for fast lookups.
    /// </summary>
    [Required]
    [StringLength(4, MinimumLength = 4)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Poll question text.
    /// </summary>
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Whether participants can select one or multiple options.
    /// </summary>
    [Required]
    public ChoiceMode ChoiceMode { get; set; }

    /// <summary>
    /// Poll status - whether voting is still open.
    /// </summary>
    public bool IsClosed { get; set; } = false;

    /// <summary>
    /// When the poll was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the poll was closed (null if still open).
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Whether the poll has been archived (moved to archive page).
    /// </summary>
    public bool IsArchived { get; set; } = false;

    /// <summary>
    /// Navigation property: Options for this poll.
    /// </summary>
    public ICollection<Option> Options { get; set; } = new List<Option>();

    /// <summary>
    /// Navigation property: Votes submitted for this poll.
    /// </summary>
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}

/// <summary>
/// Enum representing single vs multi-choice poll modes.
/// </summary>
public enum ChoiceMode
{
    /// <summary>
    /// Participants can select only one option.
    /// </summary>
    Single = 0,

    /// <summary>
    /// Participants can select multiple options.
    /// </summary>
    Multi = 1
}
