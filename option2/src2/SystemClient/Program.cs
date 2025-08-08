#nullable enable
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class SystemClient
{
    private readonly string _clientId;
    private readonly string _serverHost;
    private readonly int _serverPort;
    private TcpClient? _client;
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();

    public SystemClient(string clientId, string serverHost, int serverPort)
    {
        _clientId = clientId;
        _serverHost = serverHost;
        _serverPort = serverPort;
    }

    public async Task StartAsync()
    {
        Console.WriteLine($"[{_clientId}] Starting...");
        Console.CancelKeyPress += (s, e) => { 
            if (e != null) e.Cancel = true; 
            _cts.Cancel(); 
        };

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await ConnectAndRegisterAsync();
                Console.WriteLine($"[{_clientId}] Connected and registered successfully.");

                var listeningTask = ListenForCommandsAsync();
                var statusUpdateTask = SendStatusUpdatesAsync();

                await Task.WhenAny(listeningTask, statusUpdateTask);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_clientId}] Connection error: {ex.Message}");
            }
            finally
            {
                _client?.Close();
                Console.WriteLine($"[{_clientId}] Disconnected. Reconnecting in 10 seconds...");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), _cts.Token);
                }
                catch (TaskCanceledException)
                {
                    // 这在应用程序关闭时是预期的。
                }
            }
        }
        Console.WriteLine($"[{_clientId}] Shutting down.");
    }

    private async Task ConnectAndRegisterAsync()
    {
        _client = new TcpClient();
        await _client.ConnectAsync(_serverHost, _serverPort);
        var stream = _client.GetStream();
        _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
        _reader = new StreamReader(stream, Encoding.UTF8);

        var registerRequest = new JObject
        {
            ["MessageType"] = "RegisterRequest",
            ["CorrelationId"] = Guid.NewGuid().ToString(),
            ["Payload"] = new JObject { ["ClientId"] = _clientId }
        };

        await _writer.WriteLineAsync(registerRequest.ToString(Formatting.None));

        var responseLine = await _reader.ReadLineAsync() ?? throw new IOException("Server did not respond to registration.");
        var response = JObject.Parse(responseLine);
        if (response["Payload"]?["Success"]?.Value<bool>() != true)
        {
            throw new Exception($"Registration failed: {response["Payload"]?["Message"]}");
        }
    }

    private async Task SendStatusUpdatesAsync()
    {
        var random = new Random();
        while (!_cts.IsCancellationRequested && _client?.Connected == true)
        {
            var status = new JObject
            {
                ["cpu"] = Math.Round(random.NextDouble() * 100, 2),
                ["memory"] = random.Next(512, 4096),
                ["status"] = "Running"
            };

            var statusUpdate = new JObject
            {
                ["MessageType"] = "StatusUpdateRequest",
                ["Payload"] = new JObject
                {
                    ["ClientId"] = _clientId,
                    ["Status"] = status
                }
            };

            await _writer!.WriteLineAsync(statusUpdate.ToString(Formatting.None));
            Console.WriteLine($"[{_clientId}] Sent status update: {status.ToString(Formatting.None)}");

            await Task.Delay(TimeSpan.FromSeconds(10), _cts.Token);
        }
    }

    private async Task ListenForCommandsAsync()
    {
        while (!_cts.IsCancellationRequested && _client?.Connected == true)
        {
            var messageLine = await _reader!.ReadLineAsync(_cts.Token);
            if (messageLine == null) continue; // 服务器断开连接

            var message = JObject.Parse(messageLine);
            if (message["MessageType"]?.ToString() == "CommandRequest")
            {
                Console.WriteLine($"[{_clientId}] Received command: {message.ToString(Formatting.None)}");
                await HandleCommandAsync(message);
            }
        }
    }

    private async Task HandleCommandAsync(JObject commandRequest)
    {
        var correlationId = commandRequest["CorrelationId"]?.ToString();
        var command = commandRequest["Payload"]?["Command"]?.ToString();
        JObject resultPayload;

        if (command == "get_diagnostics")
        {
            resultPayload = new JObject
            {
                ["Success"] = true,
                ["Result"] = new JObject
                {
                    ["diskSpace"] = "85%",
                    ["uptime"] = "12 days"
                }
            };
        }
        else
        {
            resultPayload = new JObject
            {
                ["Success"] = false,
                ["Result"] = $"Unknown command: {command}"
            };
        }

        var response = new JObject
        {
            ["MessageType"] = "CommandResponse",
            ["CorrelationId"] = correlationId,
            ["Payload"] = new JObject
            {
                ["SourceClientId"] = _clientId,
                ["Success"] = resultPayload["Success"],
                ["Result"] = resultPayload["Result"]
            }
        };

        await _writer!.WriteLineAsync(response.ToString(Formatting.None));
        Console.WriteLine($"[{_clientId}] Sent command response.");
    }

    public static async Task Main(string[] args)
    {
        // 您可以通过更改 ClientId 来运行此客户端的多个实例。
        var clientId = args.Length > 0 ? args[0] : "System-A";
        var client = new SystemClient(clientId, "127.0.0.1", 8888);
        await client.StartAsync();
    }
}