using PollPoll.Services;

namespace PollPoll.BackgroundServices;

/// <summary>
/// Background service for US5: Auto-closes polls that are 7+ days old
/// Runs every hour to check for expired polls
/// </summary>
public class PollExpirationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PollExpirationService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public PollExpirationService(IServiceProvider serviceProvider, ILogger<PollExpirationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Poll Expiration Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AutoCloseExpiredPolls(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while auto-closing expired polls");
            }

            // Wait for the next check interval
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Poll Expiration Service stopped");
    }

    private async Task AutoCloseExpiredPolls(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var pollService = scope.ServiceProvider.GetRequiredService<PollService>();

        var closedCount = await pollService.AutoCloseExpiredPollsAsync();

        if (closedCount > 0)
        {
            _logger.LogInformation("Auto-closed {ClosedCount} expired poll(s)", closedCount);
        }
    }
}
