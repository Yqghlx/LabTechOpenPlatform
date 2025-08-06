using StackExchange.Redis;
using CentralHub.Garnet;

var builder = WebApplication.CreateBuilder(args);

// --- 依赖注入部分 ---
const string GarnetConnectionString = "localhost:6379";
// 将 IConnectionMultiplexer 注册为单例，确保整个应用共享一个 Garnet 连接。
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(GarnetConnectionString)
);

// 注册用于监听状态更新的后台服务。
builder.Services.AddHostedService<StateUpdateListener>();

var app = builder.Build();

// --- API 端点 (Minimal API) ---

// GET /api/systems/{systemId}/status
// 此端点取代了 Node.js 版本中的 app.get('/api/systems/:systemId/status', ...)。
app.MapGet("/api/systems/{systemId}/status", async (string systemId, IConnectionMultiplexer garnet) =>
{
    var db = garnet.GetDatabase();
    var key = $"system:{systemId}";
    var state = await db.StringGetAsync(key);

    if (state.IsNullOrEmpty)
    {
        return Results.NotFound(new { error = "未找到系统，或尚未收到任何状态。" });
    }

    // 直接返回从适配器接收到的原始 JSON 字符串。
    return Results.Content(state!, "application/json");
});

// POST /api/systems/{systemId}/control
// 此端点取代了 Node.js 版本中的 app.post('/api/systems/:systemId/control', ...)。
app.MapPost("/api/systems/{systemId}/control", async (string systemId, ControlCommand command, IConnectionMultiplexer garnet) =>
{
    if (string.IsNullOrEmpty(command.Action))
    {
        return Results.BadRequest(new { error = "无效的指令格式，'action' 字段是必需的。" });
    }

    var subscriber = garnet.GetSubscriber();
    var controlChannel = $"{Channels.ControlCommands}:{systemId}";
    command.SystemId = systemId; // 确保 systemId 在消息体中。

    var message = System.Text.Json.JsonSerializer.Serialize(command);

    // 向特定于 systemId 的控制频道发布指令。
    await subscriber.PublishAsync(new RedisChannel(controlChannel, RedisChannel.PatternMode.Literal), message);
    
    app.Logger.LogInformation("已向 '{SystemId}' 的频道发送指令 '{Action}'。", command.Action, systemId);

    return Results.Ok(new { message = "指令已发送。" });
});

app.Run();