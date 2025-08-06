using StackExchange.Redis;
using System.Text.Json;

// --- 1. 配置信息 ---
const string GarnetConnectionString = "localhost:6379";
const string SystemId = "system-B-aspnet"; // 为此适配器提供一个新的、唯一的ID

Console.WriteLine($"[{SystemId}] ASP.NET 状态适配器正在启动...");

// --- 2. 连接到 Garnet ---
try
{
    var garnet = await ConnectionMultiplexer.ConnectAsync(GarnetConnectionString);
    var subscriber = garnet.GetSubscriber();

    // --- 3. 启动一个后台任务，用于周期性地发布状态 ---
    _ = Task.Run(() => PublishStateLoop(subscriber));

    // --- 4. 订阅专属的控制指令频道 ---
    string controlChannel = $"control_commands:{SystemId}";
    await subscriber.SubscribeAsync(new RedisChannel(controlChannel, RedisChannel.PatternMode.Literal), (channel, message) =>
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[{SystemId}] 收到指令: {message}");
        Console.ResetColor();
        // TODO: 在此处添加控制真实系统的逻辑。
    });

    Console.WriteLine($"[{SystemId}] 已连接到 Garnet。");
    Console.WriteLine($"[{SystemId}] 已订阅指令频道: {controlChannel}");
    Console.WriteLine("按 [Enter] 键退出程序。");
    Console.ReadLine();
}
catch (RedisConnectionException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"无法连接到 Garnet。请确保它正在 '{GarnetConnectionString}' 上运行。错误信息: {ex.Message}");
    Console.ResetColor();
}

// 这个循环在后台运行，每5秒发布一次状态。
static async Task PublishStateLoop(ISubscriber publisher)
{
    var random = new Random();
    while (true)
    {
        var state = new
        {
            SystemId,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Status = "online",
            Metrics = new Dictionary<string, object>
            {
                { "temperature", random.Next(20, 45) },
                { "pressure", random.NextDouble().ToString("F3") }
            }
        };

        string jsonState = JsonSerializer.Serialize(state);
        // 向公共的 "state_updates" 频道发布状态
        await publisher.PublishAsync(new RedisChannel("state_updates", RedisChannel.PatternMode.Literal), jsonState);
        Console.WriteLine($"[{SystemId}] 状态更新已发送。温度: {state.Metrics["temperature"]}");

        await Task.Delay(5000);
    }
}