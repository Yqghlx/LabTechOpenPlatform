using StackExchange.Redis;

namespace CentralHub.Garnet;

// This background service is responsible for listening to the state_updates channel.
// It's the ASP.NET Core equivalent of the `setupStateListener` function in the Node.js version.
public class StateUpdateListener : BackgroundService
{
    private readonly ILogger<StateUpdateListener> _logger;
    private readonly IConnectionMultiplexer _garnet;

    public StateUpdateListener(ILogger<StateUpdateListener> logger, IConnectionMultiplexer garnet)
    {
        _logger = logger;
        _garnet = garnet;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("State Update Listener is starting.");

        var subscriber = _garnet.GetSubscriber();
        var db = _garnet.GetDatabase();

        await subscriber.SubscribeAsync(new RedisChannel(Channels.StateUpdates, RedisChannel.PatternMode.Literal), async (channel, message) =>
        {
            try
            {
                var state = System.Text.Json.JsonSerializer.Deserialize<SystemState>(message!);
                if (state?.SystemId is null)
                {
                    _logger.LogWarning("Received a state update without a SystemId.");
                    return;
                }

                var key = $"system:{state.SystemId}";
                // Store the latest state. The TTL (Time-To-Live) of 1 hour cleans up stale systems.
                await db.StringSetAsync(key, message, TimeSpan.FromHours(1));
                _logger.LogInformation("Processed state update for: {SystemId}", state.SystemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing state update message.");
            }
        });

        _logger.LogInformation("Listening for state updates on channel: {ChannelName}", Channels.StateUpdates);

        // Keep the service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}

// Shared classes for data consistency
public static class Channels
{
    public const string StateUpdates = "state_updates";
    public const string ControlCommands = "control_commands";
}

public class SystemState
{
    public string? SystemId { get; set; }
    public string? Timestamp { get; set; }
    public string? Status { get; set; }
}

public class ControlCommand
{
    public string? Action { get; set; }
    public string? SystemId { get; set; }
}
