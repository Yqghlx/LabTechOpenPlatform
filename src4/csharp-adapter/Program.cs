// Program.cs

using StackExchange.Redis; // This client works with Garnet
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
    const string GarnetConnectionString = "localhost:6379"; // Garnet server address (default port)
    const string SystemId = "system-C-sharp-garnet"; // A new name for this C# adapter

    static async Task Main(string[] args)
    {
        Console.WriteLine($"[C# Adapter: {SystemId}] 正在启动...");

        // Connect to Garnet
        try 
        {
            // The ConnectionMultiplexer can connect to any RESP-compatible server, including Garnet.
            var garnet = await ConnectionMultiplexer.ConnectAsync(GarnetConnectionString);
            var subscriber = garnet.GetSubscriber(); // Get the pub/sub operator

            // --- 3. Start a background task to periodically publish state ---
            _ = Task.Run(() => PublishStateLoop(subscriber));

            // --- 4. Subscribe to the dedicated control command channel ---
            string controlChannel = $"{Channels.ControlCommands}:{SystemId}";
            await subscriber.SubscribeAsync(new RedisChannel(controlChannel, RedisChannel.PatternMode.Literal), (channel, message) =>
            {
                // Callback executed when a message is received
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"
[{SystemId}] 收到控制指令: {message}");
                Console.ResetColor();

                // TODO: Add your logic to control the real system here
            });

            Console.WriteLine($"[{SystemId}] 已连接到 Garnet。");
            Console.WriteLine($"[{SystemId}] 正在向频道 '{Channels.StateUpdates}' 发送状态...");
            Console.WriteLine($"[{SystemId}] 正在监听频道 '{controlChannel}' 的指令...");
            Console.WriteLine("
按 [Enter] 键退出程序。");
            Console.ReadLine();
        }
        catch (RedisConnectionException ex) // The exception type is still from the Redis client library
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"无法连接到 Garnet。请确保 Garnet 正在运行于 '{GarnetConnectionString}'。错误信息: {ex.Message}");
            Console.ResetColor();
        }
    }

    // Loop for periodically sending state
    static async Task PublishStateLoop(ISubscriber subscriber)
    {
        var random = new Random();
        while (true)
        {
            // Simulate getting system state
            var state = new SystemState
            {
                SystemId = SystemId,
                Timestamp = DateTime.UtcNow.ToString("o"), // ISO 8601 format
                Metrics = new Dictionary<string, object>
                {
                    { "cpu", random.NextDouble().ToString("F2") },
                    { "memory", random.NextDouble().ToString("F2") },
                    { "threadCount", random.Next(10, 100) }
                }
            };

            // Serialize the state object to a JSON string
            string jsonState = JsonSerializer.Serialize(state);

            // Publish the message to the public state channel
            await subscriber.PublishAsync(new RedisChannel(Channels.StateUpdates, RedisChannel.PatternMode.Literal), jsonState);

            Console.WriteLine($"[{SystemId}] 已发送状态更新。CPU: {state.Metrics["cpu"]}");

            // Wait for 5 seconds
            await Task.Delay(5000);
        }
    }
}
