using StackExchange.Redis;
using CentralHub.Garnet;
using Serilog;

// 配置 Serilog 以便在应用程序启动失败时也能记录日志
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("正在启动 Central Hub");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, configuration) => 
        configuration.ReadFrom.Configuration(context.Configuration));

    var garnetConnectionString = builder.Configuration.GetConnectionString("Garnet");

    builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
        ConnectionMultiplexer.Connect(garnetConnectionString!)
    );

    builder.Services.AddHostedService<StateUpdateListener>();

    var app = builder.Build();

    app.MapGet("/api/systems/{systemId}/status", async (string systemId, IConnectionMultiplexer garnet, ILogger<Program> logger) =>
    {
        var db = garnet.GetDatabase();
        var key = $"system:{systemId}";
        var state = await db.StringGetAsync(key);

        if (state.IsNullOrEmpty)
        {   
            logger.LogWarning("未找到 systemId 为 '{SystemId}' 的状态。", systemId);
            return Results.NotFound(new { error = "未找到系统，或尚未收到任何状态。" });
        }

        logger.LogInformation("成功检索到 systemId 为 '{SystemId}' 的状态。", systemId);
        return Results.Content(state!, "application/json");
    });

    app.MapPost("/api/systems/{systemId}/control", async (string systemId, Dictionary<string, object> command, IConnectionMultiplexer garnet) =>
    {
        var subscriber = garnet.GetSubscriber();
        var controlChannel = $"{Channels.ControlCommands}:{systemId}";

        // 将 systemId 添加到命令字典中，以便适配器可以识别命令来源
        command["SystemId"] = systemId;

        var message = System.Text.Json.JsonSerializer.Serialize(command);

        await subscriber.PublishAsync(new RedisChannel(controlChannel, RedisChannel.PatternMode.Literal), message);
        
        app.Logger.LogInformation("向频道 {Channel} 发送了控制命令。", controlChannel);

        return Results.Ok(new { message = "指令已发送。" });
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "应用程序启动失败");
}
finally
{
    Log.CloseAndFlush();
}
