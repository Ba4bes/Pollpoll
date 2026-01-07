namespace PollPoll.Models;

/// <summary>
/// Results for a poll including vote counts and percentages
/// </summary>
public class PollResults
{
    public required string PollCode { get; init; }
    public required string Question { get; init; }
    public required bool IsClosed { get; init; }
    public required int TotalVotes { get; init; }
    public required List<OptionResult> Options { get; init; }
}

/// <summary>
/// Vote count and percentage for a single option
/// </summary>
public class OptionResult
{
    public required int OptionId { get; init; }
    public required string Text { get; init; }
    public required int VoteCount { get; init; }
    public required double Percentage { get; init; }
}
