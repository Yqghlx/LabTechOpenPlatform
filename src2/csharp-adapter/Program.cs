// Program.cs

using StackExchange.Redis;
using System.Text.Json;

// --- 1. 配置信息 (必须和 Node.js Hub 保持一致) ---
const string RedisConnectionString = "localhost:6379"; // Redis 服务器地址
const string SystemId = "system-C-sharp"; // 为这个 C# 适配器取一个新名字

// 定义频道名称
class Channels
{
    public const string StateUpdates = "state_updates";
    public const string ControlCommands = "control_commands";
}

// 定义状态数据结构
class SystemState
{
    public string SystemId { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public string Status { get; set; } = "online";
    public Dictionary<string, object> Metrics { get; set; } = new();
}

// --- 2. 主程序 ---
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine($"[C# Adapter: {SystemId}] 正在启动...");

        // 连接到 Redis
        try 
        {
            var redis = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);
            var subscriber = redis.GetSubscriber(); // 获取发布/订阅操作器

            // --- 3. 启动一个后台任务，用于周期性地发布状态 ---
            _ = Task.Run(() => PublishStateLoop(subscriber));

            // --- 4. 订阅专属的控制指令频道 ---
            string controlChannel = $"{Channels.ControlCommands}:{SystemId}";
            await subscriber.SubscribeAsync(controlChannel, (channel, message) =>
            {
                // 当收到消息时，执行此回调
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n[{SystemId}] 收到控制指令: {message}");
                Console.ResetColor();

                // TODO: 在这里添加您控制真实系统的逻辑
                // 例如: 解析 message JSON, 根据 action 执行不同操作
            });

            Console.WriteLine($"[{SystemId}] 已连接到 Redis。");
            Console.WriteLine($"[{SystemId}] 正在向频道 '{Channels.StateUpdates}' 发送状态...");
            Console.WriteLine($"[{SystemId}] 正在监听频道 '{controlChannel}' 的指令...");
            Console.WriteLine("\n按 [Enter] 键退出程序。");
            Console.ReadLine();
        }
        catch (RedisConnectionException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"无法连接到 Redis。请确保 Redis 正在运行于 '{RedisConnectionString}'。错误信息: {ex.Message}");
            Console.ResetColor();
        }
    }

    // 周期性发送状态的循环
    static async Task PublishStateLoop(ISubscriber subscriber)
    {
        var random = new Random();
        while (true)
        {
            // 模拟获取系统状态
            var state = new SystemState
            {
                SystemId = SystemId,
                Timestamp = DateTime.UtcNow.ToString("o"), // ISO 8601 格式
                Metrics = new Dictionary<string, object>
                {
                    { "cpu", random.NextDouble().ToString("F2") },
                    { "memory", random.NextDouble().ToString("F2") },
                    { "threadCount", random.Next(10, 100) }
                }
            };

            // 将状态对象序列化为 JSON 字符串
            string jsonState = JsonSerializer.Serialize(state);

            // 向公共的状态频道发布消息
            await subscriber.PublishAsync(Channels.StateUpdates, jsonState);

            Console.WriteLine($"[{SystemId}] 已发送状态更新。CPU: {state.Metrics["cpu"]}");

            // 等待5秒
            await Task.Delay(5000);
        }
    }
}