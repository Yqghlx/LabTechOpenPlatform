
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

namespace SocketServerLogic; // 使用新的命名空间

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

        public event Action<string>? OnLogMessage;

    public SocketServer(string ipAddress, int port)
    {
        _listener = new TcpListener(IPAddress.Parse(ipAddress), port);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        Log($"服务器已启动，正在监听: {_listener.LocalEndpoint}...");

        // 关联外部的取消 token
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_serverCts.Token, cancellationToken);

        try
        {
            while (!linkedCts.Token.IsCancellationRequested)
            {
                TcpClient tcpClient = await _listener.AcceptTcpClientAsync(linkedCts.Token);
                Log($"新的客户端已连接: {tcpClient.Client.RemoteEndPoint}");
                _ = HandleClientConnectionAsync(tcpClient, linkedCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Log("服务器关闭已启动。");
        }
        catch (Exception ex)
        {
            Log($"监听循环中发生错误: {ex.Message}");
        }
        finally
        {
            await Task.WhenAll(_clients.Values.Select(c => c.Cts.CancelAsync().ContinueWith(_ => Task.CompletedTask)));
            _listener.Stop();
            Log("服务器已完全停止。");
        }
    }

    private async Task HandleClientConnectionAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        ManagedClient? managedClient = null;
        try
        {
            using (var reader = new StreamReader(tcpClient.GetStream(), Encoding.UTF8))
            {
                var registrationLine = await reader.ReadLineAsync(cancellationToken);
                if (registrationLine == null) return;

                managedClient = await ProcessRegistrationAsync(registrationLine, tcpClient);
                if (managedClient == null) return;

                await ProcessMessagesAsync(managedClient, reader, cancellationToken);
            }
        }
        catch (IOException ex) when (ex.InnerException is SocketException)
        {
            Log($"客户端 {(managedClient?.ClientId ?? "<unregistered>")} 已断开: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            Log($"客户端 {(managedClient?.ClientId ?? "<unregistered>")} 的连接已被取消。");
        }
        catch (Exception ex)
        {
            Log($"客户端 {(managedClient?.ClientId ?? "<unregistered>")} 发生错误: {ex.Message}");
        }
        finally
        {
            if (managedClient != null)
            {
                _clients.TryRemove(managedClient.ClientId, out _);
                Log($"客户端 {managedClient.ClientId} 已清理。");
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
                await SendErrorAsync(tcpClient, "", "注册失败: 第一条消息必须是 RegisterRequest。");
                return null;
            }

            var clientId = message["Payload"]?["ClientId"]?.ToString();
            if (string.IsNullOrEmpty(clientId))
            {
                await SendErrorAsync(tcpClient, message["CorrelationId"]?.ToString() ?? string.Empty, "注册失败: ClientId 不能为空。");
                return null;
            }

            var newClient = new ManagedClient(clientId, tcpClient);
            if (!_clients.TryAdd(clientId, newClient))
            {
                if (_clients.TryRemove(clientId, out var oldClient))
                {
                    oldClient?.Client.Close();
                }
                _clients.TryAdd(clientId, newClient);
                Log($"客户端 {clientId} 重新连接，已关闭旧的会话。");
            }
            else
            {
                Log($"客户端 {clientId} 注册成功。");
            }

            var response = new JObject
            {
                ["MessageType"] = "RegisterResponse",
                ["CorrelationId"] = message["CorrelationId"],
                ["Payload"] = new JObject { ["Success"] = true, ["Message"] = "注册成功。" }
            };
            await newClient.Writer.WriteLineAsync(response.ToString(Formatting.None));
            return newClient;
        }
        catch (JsonException)
        {
            await SendErrorAsync(tcpClient, "", "注册失败: 无效的 JSON 格式。");
            return null;
        }
    }

    private async Task ProcessMessagesAsync(ManagedClient client, StreamReader reader, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !client.Cts.IsCancellationRequested && client.Client.Connected)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;

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
                        await SendErrorAsync(client, message["CorrelationId"]?.ToString() ?? string.Empty, $"未知的 MessageType: {messageType}");
                        break;
                }
            }
            catch (JsonException) { await SendErrorAsync(client, "", "无效的 JSON 格式。"); }
            catch (Exception ex) { Log($"处理来自 {client.ClientId} 的消息时出错: {ex.Message}"); }
        }
    }

    private async Task HandleStatusQueryAsync(ManagedClient requester, JObject query)
    {
        var targetId = query["Payload"]?["TargetClientId"]?.ToString();
        var correlationId = query["CorrelationId"]?.ToString();
        JObject responsePayload;

        if (string.IsNullOrEmpty(targetId))
        {
            responsePayload = new JObject { ["Found"] = false, ["Message"] = "TargetClientId 不能为空。" };
        }
        else if (_statusCache.TryGetValue(targetId, out var status))
        {
            responsePayload = new JObject { ["Found"] = true, ["ClientId"] = targetId, ["Status"] = status };
        }
        else
        {
            responsePayload = new JObject { ["Found"] = false, ["Message"] = $"客户端 {targetId} 未找到或尚未报告状态。" };
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
                _commandTracker[correlationId] = sourceClient.ClientId;
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(correlationId) && _commandTracker.TryRemove(correlationId, out var originalRequesterId))
            {
                targetClientId = originalRequesterId;
            }
            else
            {
                targetClientId = null;
            }
        }

        if (targetClientId != null && _clients.TryGetValue(targetClientId, out var targetClient))
        {
            await targetClient.Writer.WriteLineAsync(message.ToString(Formatting.None));
        }
        else if (isCommand)
        {
            await SendErrorAsync(sourceClient, correlationId ?? string.Empty, $"目标客户端 {targetClientId} 未连接。");
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

    private void Log(string message)
    {
        OnLogMessage?.Invoke(message);
        System.Diagnostics.Debug.WriteLine(message); // 保留 Debug 输出
    }
}
