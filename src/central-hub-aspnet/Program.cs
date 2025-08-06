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

    app.MapGet("/api/systems/{systemId}/status", async (string systemId, IConnectionMultiplexer garnet) =>
    {
        var db = garnet.GetDatabase();
        var key = $"system:{systemId}";
        var state = await db.StringGetAsync(key);

        if (state.IsNullOrEmpty)
        {
            return Results.NotFound(new { error = "未找到系统，或尚未收到任何状态。" });
        }

        return Results.Content(state!, "application/json");
    });

    app.MapPost("/api/systems/{systemId}/control", async (string systemId, ControlCommand command, IConnectionMultiplexer garnet) =>
    {
        if (string.IsNullOrEmpty(command.Action))
        {
            return Results.BadRequest(new { error = "无效的指令格式，'action' 字段是必需的。" });
        }

        var subscriber = garnet.GetSubscriber();
        var controlChannel = $"{Channels.ControlCommands}:{systemId}";
        command.SystemId = systemId;

        var message = System.Text.Json.JsonSerializer.Serialize(command);

        await subscriber.PublishAsync(new RedisChannel(controlChannel, RedisChannel.PatternMode.Literal), message);
        
        app.Logger.LogInformation("已向 '{SystemId}' 的频道发送指令 '{Action}'。", command.Action, systemId);

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
