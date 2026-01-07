using Microsoft.AspNetCore.SignalR;

namespace PollPoll.Hubs;

/// <summary>
/// SignalR hub for broadcasting live vote updates to results page viewers
/// </summary>
public class ResultsHub : Hub
{
    private readonly ILogger<ResultsHub> _logger;
    private readonly Dictionary<string, DateTime> _lastBroadcast = new();
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromSeconds(1);

    public ResultsHub(ILogger<ResultsHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Join a poll group to receive updates for a specific poll
    /// </summary>
    /// <param name="pollCode">Poll code to join</param>
    public async Task JoinPollGroup(string pollCode)
    {
        var upperCode = pollCode.ToUpper();
        await Groups.AddToGroupAsync(Context.ConnectionId, upperCode);
        _logger.LogInformation("Client {ConnectionId} joined poll group {PollCode}", Context.ConnectionId, upperCode);
    }

    /// <summary>
    /// Broadcast vote update to all clients in poll group (with throttling)
    /// </summary>
    /// <param name="pollCode">Poll code that was updated</param>
    public async Task VoteUpdated(string pollCode)
    {
        var upperCode = pollCode.ToUpper();

        // Throttle broadcasts to max 1 per second per poll (PERF-009)
        lock (_lastBroadcast)
        {
            if (_lastBroadcast.TryGetValue(upperCode, out var lastTime))
            {
                var elapsed = DateTime.UtcNow - lastTime;
                if (elapsed < ThrottleInterval)
                {
                    _logger.LogDebug("Throttling broadcast for poll {PollCode}, last broadcast {Elapsed}ms ago",
                        upperCode, elapsed.TotalMilliseconds);
                    return;
                }
            }
            _lastBroadcast[upperCode] = DateTime.UtcNow;
        }

        _logger.LogInformation("Broadcasting vote update for poll {PollCode}", upperCode);
        await Clients.Group(upperCode).SendAsync("VoteUpdated", upperCode);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
