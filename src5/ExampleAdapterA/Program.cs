using LabTech.GarnetAdapter;
using System.Text.Json;

const string GarnetConnectionString = "localhost:6379";
const string SystemId = "system-Example-A";

Console.WriteLine("--- 示例适配器 A ---");

// 1. 创建适配器客户端实例
var adapter = await GarnetAdapterClient.CreateAsync(GarnetConnectionString, SystemId);

// 2. 定义如何生成状态 (业务逻辑)
adapter.StateGenerator = () => 
{
    var random = new Random();
    // 返回一个匿名对象，它将被自动序列化为 JSON
    return new 
    {
        SystemId, // 直接使用外部定义的 SystemId
        Timestamp = DateTime.UtcNow.ToString("o"),
        Status = "online",
        Metrics = new 
        {
            CpuUsage = $"{random.NextDouble() * 100:F2}%",
            MemoryUsage = $"{random.Next(500, 4096)}MB"
        }
    };
};

// 3. 定义如何处理指令 (业务逻辑)
adapter.OnCommandReceived = (message) => 
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"\n[{SystemId}] 收到指令: {message}");
    // 可以使用 JsonSerializer.Deserialize 来解析指令
    Console.ResetColor();
};

// 4. 运行适配器
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => 
{
    Console.WriteLine("正在关闭适配器...");
    cts.Cancel();
    e.Cancel = true;
};

await adapter.RunAsync(cts.Token);
