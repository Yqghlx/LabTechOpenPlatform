// Program.cs

using StackExchange.Redis; // 这个客户端库同样兼容 Garnet
using System.Text.Json;

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
partial class Program
{
    // --- 1. 配置信息 ---
    const string GarnetConnectionString = "localhost:6379"; // Garnet 服务器地址
    const string SystemId = "system-C-sharp-garnet"; // 为这个 C# 适配器取一个新名字

    static async Task Main(string[] args)
    {
        Console.WriteLine($"[C# 适配器: {SystemId}] 正在启动...");

        // 连接到 Garnet
        try 
        {
            // ConnectionMultiplexer 可以连接到任何兼容 RESP 协议的服务器，包括 Garnet。
            var garnet = await ConnectionMultiplexer.ConnectAsync(GarnetConnectionString);
            var subscriber = garnet.GetSubscriber(); // 获取发布/订阅操作器

            // --- 3. 启动一个后台任务，用于周期性地发布状态 ---
            _ = Task.Run(() => PublishStateLoop(subscriber));

            // --- 4. 订阅专属的控制指令频道 ---
            string controlChannel = $"{Channels.ControlCommands}:{SystemId}";
            await subscriber.SubscribeAsync(new RedisChannel(controlChannel, RedisChannel.PatternMode.Literal), (channel, message) =>
            {
                // 当收到消息时，执行此回调
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n[{SystemId}] 收到控制指令: {message}");
                Console.ResetColor();

                // TODO: 在这里添加您控制真实系统的逻辑
            });

            Console.WriteLine($"[{SystemId}] 已连接到 Garnet。");
            Console.WriteLine($"[{SystemId}] 正在向频道 '{Channels.StateUpdates}' 发送状态...");
            Console.WriteLine($"[{SystemId}] 正在监听频道 '{controlChannel}' 的指令...");
            Console.WriteLine("\n按 [Enter] 键退出程序。");
            Console.ReadLine();
        }
        catch (RedisConnectionException ex) // 异常类型仍然来自 Redis 客户端库
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"无法连接到 Garnet。请确保 Garnet 正在运行于 '{GarnetConnectionString}'。错误信息: {ex.Message}");
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
            await subscriber.PublishAsync(new RedisChannel(Channels.StateUpdates, RedisChannel.PatternMode.Literal), jsonState);

            Console.WriteLine($"[{SystemId}] 已发送状态更新。CPU: {state.Metrics["cpu"]}");

            // 等待5秒
            await Task.Delay(5000);
        }
    }
}