using System.ComponentModel.DataAnnotations;

namespace PollPoll.Models;

/// <summary>
/// Represents a single answer choice within a poll.
/// </summary>
public class Option
{
    /// <summary>
    /// Auto-increment primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to Poll.
    /// </summary>
    [Required]
    public int PollId { get; set; }

    /// <summary>
    /// Option text (e.g., "Red", "Blue", "Green").
    /// </summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Order for display (0-based, sequential).
    /// </summary>
    [Required]
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Navigation property: The poll this option belongs to.
    /// </summary>
    public Poll Poll { get; set; } = null!;

    /// <summary>
    /// Navigation property: Votes submitted for this option.
    /// </summary>
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
