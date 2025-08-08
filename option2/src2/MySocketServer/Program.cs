#nullable enable
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// 代表一个已成功注册的连接客户端。
public class ManagedClient
{
    public string ClientId { get; }
    public TcpClient Client { get; }
    public StreamWriter Writer { get; }
    public CancellationTokenSource Cts { get; } = new CancellationTokenSource();

    public ManagedClient(string clientId, TcpClient client)
    {
        ClientId = clientId;
        Client = client;
        var stream = client.GetStream();
        Writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
    }
}

public class SocketServer
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _serverCts = new CancellationTokenSource();

    // 所有已连接并注册的客户端的主注册表
    private readonly ConcurrentDictionary<string, ManagedClient> _clients = new ConcurrentDictionary<string, ManagedClient>();
    
    // 缓存系统客户端报告的最新状态
    private readonly ConcurrentDictionary<string, JObject> _statusCache = new ConcurrentDictionary<string, JObject>();

    // 跟踪哪个控制客户端正在等待特定命令的响应
    private readonly ConcurrentDictionary<string, string> _commandTracker = new ConcurrentDictionary<string, string>();

    public SocketServer(string ipAddress, int port)
    {
        _listener = new TcpListener(IPAddress.Parse(ipAddress), port);
    }

    public async Task StartAsync()
    {
        _listener.Start();
        Console.WriteLine($"Server started. Listening on {_listener.LocalEndpoint}...");
        Console.WriteLine("Press Ctrl+C to shut down.");
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; Stop(); };

        try
        {
            while (!_serverCts.Token.IsCancellationRequested)
            {
                TcpClient tcpClient = await _listener.AcceptTcpClientAsync(_serverCts.Token);
                Console.WriteLine($"New client connected: {tcpClient.Client.RemoteEndPoint}");
                // 不要等待，让它在后台运行
                _ = HandleClientConnectionAsync(tcpClient);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Server shutdown initiated.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred in listener loop: {ex.Message}");
        }
        finally
        {
            await Task.WhenAll(_clients.Values.Select(c => c.Cts.CancelAsync().ContinueWith(_ => Task.CompletedTask)));
            _listener.Stop();
            Console.WriteLine("Server stopped completely.");
        }
    }

    private async Task HandleClientConnectionAsync(TcpClient tcpClient)
    {
        ManagedClient? managedClient = null;
        try
        {
            using (var reader = new StreamReader(tcpClient.GetStream(), Encoding.UTF8))
            {
                // 第一条消息必须是注册请求。
                var registrationLine = await reader.ReadLineAsync(CancellationToken.None); // 初始读取没有 CancellationToken
                if (registrationLine == null) return; // 客户端过早断开连接

                managedClient = await ProcessRegistrationAsync(registrationLine, tcpClient);
                if (managedClient == null) return; // 注册失败

                // 开始处理来自此客户端的后续消息
                await ProcessMessagesAsync(managedClient, reader);
            }
        }
        catch (IOException ex) when (ex.InnerException is SocketException)
        {
            Console.WriteLine($"Client {(managedClient?.ClientId ?? "<unregistered>")} disconnected: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred with client {(managedClient?.ClientId ?? "<unregistered>")}: {ex.Message}");
        }
        finally
        {
            if (managedClient != null)
            {
                _clients.TryRemove(managedClient.ClientId, out _);
                Console.WriteLine($"Client {managedClient.ClientId} cleaned up.");
            }
            tcpClient.Close();
        }
    }

    private async Task<ManagedClient?> ProcessRegistrationAsync(string json, TcpClient tcpClient)
    {
        try
        {
            var message = JObject.Parse(json);
            if (message["MessageType"]?.ToString() != "RegisterRequest")
            {
                await SendErrorAsync(tcpClient, "", "Registration failed: First message must be RegisterRequest.");
                return null;
            }

            var clientId = message["Payload"]?["ClientId"]?.ToString();
            if (string.IsNullOrEmpty(clientId))
            {
                await SendErrorAsync(tcpClient, message["CorrelationId"]?.ToString() ?? string.Empty, "Registration failed: ClientId is missing.");
                return null;
            }

            var newClient = new ManagedClient(clientId, tcpClient);
            if (!_clients.TryAdd(clientId, newClient))
            {
                if (_clients.TryRemove(clientId, out var oldClient))
                {
                    oldClient?.Client.Close(); // 断开旧客户端
                }
                _clients.TryAdd(clientId, newClient);
                Console.WriteLine($"Client {clientId} reconnected, closing previous session.");
            }
            else
            {
                Console.WriteLine($"Client {clientId} registered successfully.");
            }

            var response = new JObject
            {
                ["MessageType"] = "RegisterResponse",
                ["CorrelationId"] = message["CorrelationId"],
                ["Payload"] = new JObject { ["Success"] = true, ["Message"] = "Registration successful." }
            };
            await newClient.Writer.WriteLineAsync(response.ToString(Formatting.None));
            return newClient;
        }
        catch (JsonException)
        {
            await SendErrorAsync(tcpClient, "", "Registration failed: Invalid JSON format.");
            return null;
        }
    }

    private async Task ProcessMessagesAsync(ManagedClient client, StreamReader reader)
    {
        while (!client.Cts.IsCancellationRequested && client.Client.Connected)
        {
            var line = await reader.ReadLineAsync(client.Cts.Token);
            if (line == null) break; // 客户端断开连接

            try
            {
                var message = JObject.Parse(line);
                var messageType = message["MessageType"]?.ToString();

                switch (messageType)
                {
                    case "StatusUpdateRequest":
                        var statusClientId = message["Payload"]?["ClientId"]?.ToString();
                        var status = message["Payload"]?["Status"] as JObject;
                        if (status != null && statusClientId == client.ClientId)
                        {
                            _statusCache[statusClientId] = status;
                            // 可选：发回一个确认
                        }
                        break;

                    case "StatusQueryRequest":
                        await HandleStatusQueryAsync(client, message);
                        break;

                    case "CommandRequest":
                        await RouteMessageToTargetAsync(client, message, isCommand: true);
                        break;

                    case "CommandResponse":
                        await RouteMessageToTargetAsync(client, message, isCommand: false);
                        break;

                    default:
                        await SendErrorAsync(client, message["CorrelationId"]?.ToString() ?? string.Empty, $"Unknown MessageType: {messageType}");
                        break;
                }
            }
            catch (JsonException) { await SendErrorAsync(client, "", "Invalid JSON format."); }
            catch (Exception ex) { Console.WriteLine($"Error processing message from {client.ClientId}: {ex.Message}"); }
        }
    }

    private async Task HandleStatusQueryAsync(ManagedClient requester, JObject query)
    {
        var targetId = query["Payload"]?["TargetClientId"]?.ToString();
        var correlationId = query["CorrelationId"]?.ToString();
        JObject responsePayload;

        if (string.IsNullOrEmpty(targetId))
        {
            responsePayload = new JObject { ["Found"] = false, ["Message"] = "TargetClientId is missing." };
        }
        else if (_statusCache.TryGetValue(targetId, out var status))
        {
            responsePayload = new JObject { ["Found"] = true, ["ClientId"] = targetId, ["Status"] = status };
        }
        else
        {
            responsePayload = new JObject { ["Found"] = false, ["Message"] = $"Client {targetId} not found or has not reported status." };
        }

        var response = new JObject
        {
            ["MessageType"] = "StatusQueryResponse",
            ["CorrelationId"] = correlationId,
            ["Payload"] = responsePayload
        };
        await requester.Writer.WriteLineAsync(response.ToString(Formatting.None));
    }

    private async Task RouteMessageToTargetAsync(ManagedClient sourceClient, JObject message, bool isCommand)
    {
        string? targetClientId;
        var correlationId = message["CorrelationId"]?.ToString();

        if (isCommand)
        {
            targetClientId = message["Payload"]?["TargetClientId"]?.ToString();
            if (!string.IsNullOrEmpty(correlationId) && !string.IsNullOrEmpty(targetClientId))
            {
                _commandTracker[correlationId] = sourceClient.ClientId; // 跟踪谁发送了命令
            }
        }
        else // 是命令响应
        {
            if (!string.IsNullOrEmpty(correlationId) && _commandTracker.TryRemove(correlationId, out var originalRequesterId))
            {
                targetClientId = originalRequesterId;
            }
            else
            {
                targetClientId = null; // 没有人可以路由到
            }
        }

        if (targetClientId != null && _clients.TryGetValue(targetClientId, out var targetClient))
        {
            await targetClient.Writer.WriteLineAsync(message.ToString(Formatting.None));
        }
        else if (isCommand)
        {
            await SendErrorAsync(sourceClient, correlationId ?? string.Empty, $"Target client {targetClientId} not connected.");
        }
    }

    private Task SendErrorAsync(ManagedClient client, string correlationId, string errorMessage)
    {
        var errorResponse = new JObject
        {
            ["MessageType"] = "ErrorResponse",
            ["CorrelationId"] = correlationId,
            ["Payload"] = new JObject { ["Message"] = errorMessage }
        };
        return client.Writer.WriteLineAsync(errorResponse.ToString(Formatting.None));
    }

    private Task SendErrorAsync(TcpClient client, string correlationId, string errorMessage)
    {
        var errorResponse = new JObject
        {
            ["MessageType"] = "ErrorResponse",
            ["CorrelationId"] = correlationId,
            ["Payload"] = new JObject { ["Message"] = errorMessage }
        };
        var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };
        return writer.WriteLineAsync(errorResponse.ToString(Formatting.None));
    }

    public void Stop()
    {
        if (!_serverCts.IsCancellationRequested)
        {
            _serverCts.Cancel();
        }
    }

    public static async Task Main(string[] args)
    {
        var server = new SocketServer("0.0.0.0", 8888);
        await server.StartAsync();
    }
}