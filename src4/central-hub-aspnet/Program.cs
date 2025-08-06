using StackExchange.Redis;
using CentralHub.Garnet;

var builder = WebApplication.CreateBuilder(args);

// --- Dependency Injection ---
const string GarnetConnectionString = "localhost:3278";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(GarnetConnectionString)
);

// Register the background service that listens for state updates
builder.Services.AddHostedService<StateUpdateListener>();

var app = builder.Build();

// --- API Endpoints (Minimal API) ---

// GET /api/systems/{systemId}/status
// This replaces the app.get('/api/systems/:systemId/status', ...) in Node.js
app.MapGet("/api/systems/{systemId}/status", async (string systemId, IConnectionMultiplexer garnet) =>
{
    var db = garnet.GetDatabase();
    var key = $"system:{systemId}";
    var state = await db.StringGetAsync(key);

    if (state.IsNullOrEmpty)
    {
        return Results.NotFound(new { error = "System not found or no state received yet." });
    }

    // Return the raw JSON string received from the adapter
    return Results.Content(state!, "application/json");
});

// POST /api/systems/{systemId}/control
// This replaces the app.post('/api/systems/:systemId/control', ...) in Node.js
app.MapPost("/api/systems/{systemId}/control", async (string systemId, ControlCommand command, IConnectionMultiplexer garnet) =>
{
    if (string.IsNullOrEmpty(command.Action))
    {
        return Results.BadRequest(new { error = "Invalid command format. 'action' is required." });
    }

    var subscriber = garnet.GetSubscriber();
    var controlChannel = $"{Channels.ControlCommands}:{systemId}";
    command.SystemId = systemId; // Ensure systemId is in the message body

    var message = System.Text.Json.JsonSerializer.Serialize(command);

    await subscriber.PublishAsync(new RedisChannel(controlChannel, RedisChannel.PatternMode.Literal), message);
    
    app.Logger.LogInformation("Sent command '{Action}' to channel for '{SystemId}'", command.Action, systemId);

    return Results.Ok(new { message = "Command sent." });
});

app.Run();
