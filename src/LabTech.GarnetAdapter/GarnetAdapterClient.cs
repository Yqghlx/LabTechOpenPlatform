using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LabTech.GarnetAdapter
{
    public class GarnetAdapterClient
    {
        private readonly string _systemId;
        private readonly ISubscriber _subscriber;
        private readonly ConnectionMultiplexer _garnet;
        private readonly ILogger _logger;

        public Func<object>? StateGenerator { get; set; }
        public Action<string>? OnCommandReceived { get; set; }

        private GarnetAdapterClient(string systemId, ConnectionMultiplexer garnet, ILogger logger)
        {
            _systemId = systemId;
            _garnet = garnet;
            _subscriber = _garnet.GetSubscriber();
            _logger = logger;
        }

        public static async Task<GarnetAdapterClient> CreateAsync(string connectionString, string systemId, ILogger? logger = null)
        {
            var safeLogger = logger ?? NullLogger.Instance;
            try
            {
                var garnet = await ConnectionMultiplexer.ConnectAsync(connectionString);
                safeLogger.LogInformation("成功连接到 Garnet 服务器。");
                return new GarnetAdapterClient(systemId, garnet, safeLogger);
            }
            catch (RedisConnectionException ex)
            {
                safeLogger.LogCritical(ex, "无法连接到 Garnet 服务器: {Message}", ex.Message);
                throw;
            }
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            if (StateGenerator is null || OnCommandReceived is null)
            {
                throw new InvalidOperationException("必须在调用 RunAsync 之前设置 StateGenerator 和 OnCommandReceived。");
            }

            _logger.LogInformation("[{SystemId}] 适配器正在启动...", _systemId);

            var publishingTask = Task.Run(() => PublishStateLoopAsync(cancellationToken), cancellationToken);

            string controlChannel = $"control_commands:{_systemId}";
            var channel = new RedisChannel(controlChannel, RedisChannel.PatternMode.Literal);

            await _subscriber.SubscribeAsync(channel, (ch, msg) =>
            {
                _logger.LogDebug("[{SystemId}] 从频道 {Channel} 收到消息。", _systemId, ch);
                OnCommandReceived.Invoke(msg!); 
            });

            _logger.LogInformation("[{SystemId}] 已订阅频道: {Channel}", _systemId, controlChannel);

            // 等待取消信号
            await cancellationToken.WaitHandle.WaitOneAsync(Timeout.Infinite, cancellationToken);

            _logger.LogInformation("[{SystemId}] 正在取消订阅并关闭...", _systemId);
            await _subscriber.UnsubscribeAsync(channel);

            // 等待发布任务完成
            await publishingTask;
            
            _logger.LogInformation("[{SystemId}] 适配器已关闭。", _systemId);
        }

        private async Task PublishStateLoopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[{SystemId}] 状态发布任务已启动。", _systemId);
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var state = StateGenerator!.Invoke();
                    string jsonState = JsonSerializer.Serialize(state);

                    await _subscriber.PublishAsync(new RedisChannel("state_updates", RedisChannel.PatternMode.Literal), jsonState);
                    _logger.LogDebug("[{SystemId}] 已发布状态更新。", _systemId);

                    await Task.Delay(5000, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // 这是预期的异常，当取消发生时，直接退出循环
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{SystemId}] 在状态发布循环中发生未处理的异常。", _systemId);
                    await Task.Delay(5000, cancellationToken); // 避免快速失败循环
                }
            }
            _logger.LogInformation("[{SystemId}] 状态发布任务已停止。", _systemId);
        }
    }

    // 扩展方法，使 CancellationToken 可以被异步等待
    internal static class CancellationTokenExtensions
    {
        public static Task WaitOneAsync(this WaitHandle waitHandle, int timeout, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            var registration = cancellationToken.Register(() => tcs.TrySetResult(true));
            ThreadPool.RegisterWaitForSingleObject(waitHandle, (state, timedOut) =>
            {
                if (!timedOut)
                    tcs.TrySetResult(true);
            }, null, timeout, true);
            return tcs.Task;
        }
    }
}