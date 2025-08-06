using StackExchange.Redis;

namespace CentralHub.Garnet;

// 这个后台服务负责监听 state_updates 频道。
// 它相当于 Node.js 版本中的 `setupStateListener` 函数的 ASP.NET Core 实现。
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
        _logger.LogInformation("状态更新监听器正在启动。");

        var subscriber = _garnet.GetSubscriber();
        var db = _garnet.GetDatabase();

        await subscriber.SubscribeAsync(new RedisChannel(Channels.StateUpdates, RedisChannel.PatternMode.Literal), async (channel, message) =>
        {
            try
            {
                var state = System.Text.Json.JsonSerializer.Deserialize<SystemState>(message!);
                if (state?.SystemId is null)
                {
                    _logger.LogWarning("收到一个没有 SystemId 的状态更新。");
                    return;
                }

                var key = $"system:{state.SystemId}";
                // 存储最新状态。1小时的生存时间（TTL）用于清理过期的系统数据。
                await db.StringSetAsync(key, message, TimeSpan.FromHours(1));
                _logger.LogInformation("已处理来自 {SystemId} 的状态更新。", state.SystemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理状态更新消息时出错。");
            }
        });

        _logger.LogInformation("正在监听频道 {ChannelName} 上的状态更新。", Channels.StateUpdates);

        // 保持服务持续运行
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}

// 用于数据一致性的共享类
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
