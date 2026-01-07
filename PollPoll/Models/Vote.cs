using System.ComponentModel.DataAnnotations;

namespace PollPoll.Models;

/// <summary>
/// Represents a participant's vote selection.
/// </summary>
public class Vote
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
    /// Foreign key to Option.
    /// </summary>
    [Required]
    public int OptionId { get; set; }

    /// <summary>
    /// Unique voter identifier from cookie.
    /// </summary>
    [Required]
    public Guid VoterId { get; set; }

    /// <summary>
    /// When the vote was cast.
    /// </summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property: The poll this vote belongs to.
    /// </summary>
    public Poll Poll { get; set; } = null!;

    /// <summary>
    /// Navigation property: The option that was voted for.
    /// </summary>
    public Option Option { get; set; } = null!;
}
