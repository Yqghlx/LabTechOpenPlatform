using LabTech.GarnetAdapter;

const string GarnetConnectionString = "localhost:6379";
const string SystemId = "system-Example-B";

Console.WriteLine("--- 示例适配器 B ---");

// 1. 创建适配器客户端实例
var adapter = await GarnetAdapterClient.CreateAsync(GarnetConnectionString, SystemId);

// 2. 定义此适配器独特的状态生成逻辑
adapter.StateGenerator = () => 
{
    var random = new Random();
    return new 
    {
        SystemId,
        Timestamp = DateTime.UtcNow.ToString("o"),
        Status = "healthy",
        Data = new 
        {
            Temperature = random.Next(20, 35),
            Humidity = $"{random.Next(40, 60)}%"
        }
    };
};

// 3. 定义此适配器独特的指令处理逻辑
adapter.OnCommandReceived = (message) => 
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"\n[{SystemId}] 执行了重启指令: {message}");
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

