using StackExchange.Redis;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LabTech.GarnetAdapter
{
    /// <summary>
    /// 一个通用的、可重用的 Garnet 适配器客户端。
    /// 封装了连接、状态发布和指令订阅的通用逻辑。
    /// </summary>
    public class GarnetAdapterClient
    {
        private readonly string _connectionString;
        private readonly string _systemId;
        private readonly ISubscriber _subscriber;
        private readonly ConnectionMultiplexer _garnet;

        /// <summary>
        /// 用于生成系统状态的委托。使用者必须提供此逻辑。
        /// </summary>
        public Func<object>? StateGenerator { get; set; }

        /// <summary>
        /// 用于处理接收到的控制指令的委托。使用者必须提供此逻辑。
        /// </summary>
        public Action<string>? OnCommandReceived { get; set; }

        private GarnetAdapterClient(string connectionString, string systemId, ConnectionMultiplexer garnet)
        {
            _connectionString = connectionString;
            _systemId = systemId;
            _garnet = garnet;
            _subscriber = _garnet.GetSubscriber();
        }

        /// <summary>
        /// 创建并初始化一个新的适配器客户端实例。
        /// </summary>
        /// <param name="connectionString">到 Garnet 服务器的连接字符串。</param>
        /// <param name="systemId">当前系统的唯一标识符。</param>
        /// <returns>一个已连接的客户端实例。</returns>
        public static async Task<GarnetAdapterClient> CreateAsync(string connectionString, string systemId)
        {
            var garnet = await ConnectionMultiplexer.ConnectAsync(connectionString);
            return new GarnetAdapterClient(connectionString, systemId, garnet);
        }

        /// <summary>
        /// 启动适配器，开始发布状态并监听指令。
        /// </summary>
        /// <param name="cancellationToken">用于停止操作的取消令牌。</param>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            if (StateGenerator is null || OnCommandReceived is null)
            {
                throw new InvalidOperationException("必须在调用 RunAsync 之前设置 StateGenerator 和 OnCommandReceived。");
            }

            Console.WriteLine($"[{_systemId}] 适配器正在启动...");

            // 1. 在后台启动状态发布循环
            var publishingTask = Task.Run(() => PublishStateLoopAsync(cancellationToken), cancellationToken);

            // 2. 在前台订阅控制指令
            string controlChannel = $"control_commands:{_systemId}";
            await _subscriber.SubscribeAsync(new RedisChannel(controlChannel, RedisChannel.PatternMode.Literal), (channel, message) =>
            {
                OnCommandReceived.Invoke(message!); // 调用使用者提供的处理逻辑
            });

            Console.WriteLine($"[{_systemId}] 已连接到 Garnet 并开始监听频道: {controlChannel}");

            // 等待直到取消操作被请求
            await publishingTask;
        }

        private async Task PublishStateLoopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"[{_systemId}] 状态发布任务已启动。每5秒发送一次更新。");
            while (!cancellationToken.IsCancellationRequested)
            {
                var state = StateGenerator!.Invoke(); // 调用使用者提供的状态生成逻辑
                string jsonState = JsonSerializer.Serialize(state);

                await _subscriber.PublishAsync(new RedisChannel("state_updates", RedisChannel.PatternMode.Literal), jsonState);
                
                await Task.Delay(5000, cancellationToken);
            }
        }
    }
}