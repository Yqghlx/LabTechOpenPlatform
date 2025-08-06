using StackExchange.Redis;
using System.Text.Json;

// --- 1. Configuration ---
const string GarnetConnectionString = "localhost:3278";
const string SystemId = "system-B-aspnet"; // A new, unique ID for this adapter

Console.WriteLine($"[{SystemId}] ASP.NET State Adapter starting...");

// --- 2. Connect to Garnet ---
try
{
    var garnet = await ConnectionMultiplexer.ConnectAsync(GarnetConnectionString);
    var subscriber = garnet.GetSubscriber();

    // --- 3. Start a background task to periodically publish state ---
    _ = Task.Run(() => PublishStateLoop(subscriber));

    // --- 4. Subscribe to the dedicated control command channel ---
    string controlChannel = $"control_commands:{SystemId}";
    await subscriber.SubscribeAsync(controlChannel, (channel, message) =>
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[{SystemId}] Received command: {message}");
        Console.ResetColor();
        // TODO: Add logic to control the actual system.
    });

    Console.WriteLine($"[{SystemId}] Connected to Garnet.");
    Console.WriteLine($"[{SystemId}] Subscribed to command channel: {controlChannel}");
    Console.WriteLine("Press [Enter] to exit.");
    Console.ReadLine();
}
catch (RedisConnectionException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Could not connect to Garnet. Ensure it's running at '{GarnetConnectionString}'. Error: {ex.Message}");
    Console.ResetColor();
}

// This loop runs in the background, publishing state every 5 seconds.
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
        await publisher.PublishAsync("state_updates", jsonState);
        Console.WriteLine($"[{SystemId}] State update sent. Temperature: {state.Metrics["temperature"]}");

        await Task.Delay(5000);
    }
}
