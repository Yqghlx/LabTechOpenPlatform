#nullable enable
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

public class ControlClient
{
    private readonly string _clientId;
    private readonly string _serverHost;
    private readonly int _serverPort;
    private TcpClient? _client;
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> _pendingRequests = new ConcurrentDictionary<string, TaskCompletionSource<JObject>>();

    public ControlClient(string clientId, string serverHost, int serverPort)
    {
        _clientId = clientId;
        _serverHost = serverHost;
        _serverPort = serverPort;
    }

    public async Task StartAsync()
    {
        Console.WriteLine("正在连接到服务器...");
        try
        {
            await ConnectAndRegisterAsync();
            Console.WriteLine("连接并注册成功。");
            Console.WriteLine("输入命令 (例如, 'status System-A', 'command System-A get_diagnostics', 或 'exit')");

            var listeningTask = ListenForResponsesAsync();
            var commandLoopTask = RunCommandLoopAsync();

            await Task.WhenAny(listeningTask, commandLoopTask);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
        }
        finally
        {
            _cts.Cancel();
            _client?.Close();
            Console.WriteLine("已断开连接。");
        }
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

        var responseLine = await _reader.ReadLineAsync() ?? throw new IOException("服务器未响应注册请求。");
        var response = JObject.Parse(responseLine);
        if (response["Payload"]?["Success"]?.Value<bool>() != true)
        {
            throw new Exception($"注册失败: {response["Payload"]?["Message"]}");
        }
    }

    private async Task ListenForResponsesAsync()
    {
        while (!_cts.IsCancellationRequested && _client?.Connected == true)
        {
            try
            {
                var messageLine = await _reader!.ReadLineAsync(_cts.Token);
                if (messageLine == null) continue;

                var message = JObject.Parse(messageLine);
                var correlationId = message["CorrelationId"]?.ToString();

                if (!string.IsNullOrEmpty(correlationId) && _pendingRequests.TryRemove(correlationId, out var tcs))
                {
                    tcs.SetResult(message);
                }
                else
                {
                    Console.WriteLine($"\n收到未经请求的消息:\n{message.ToString(Formatting.Indented)}");
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Console.WriteLine($"\n监听器错误: {ex.Message}"); break; }
        }
    }

    private async Task RunCommandLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            Console.Write("> ");
            var input = await Task.Run(() => Console.ReadLine());
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            var parts = input.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                Console.WriteLine("无效的命令格式。");
                continue;
            }

            var commandType = parts[0].ToLower();
            var targetClientId = parts[1];

            try
            {
                JObject? response = null;
                switch (commandType)
                {
                    case "status":
                        response = await SendRequestAsync(BuildStatusQuery(targetClientId));
                        break;
                    case "command":
                        if (parts.Length < 3) { Console.WriteLine("缺少命令名称。"); continue; }
                        response = await SendRequestAsync(BuildCommandRequest(targetClientId, parts[2]));
                        break;
                    default:
                        Console.WriteLine($"未知的命令类型: {commandType}");
                        continue;
                }
                Console.WriteLine($"响应:\n{response.ToString(Formatting.Indented)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送请求时出错: {ex.Message}");
            }
        }
    }

    private async Task<JObject> SendRequestAsync(JObject request)
    {
        var correlationId = request["CorrelationId"]!.ToString();
        var tcs = new TaskCompletionSource<JObject>();
        _pendingRequests[correlationId] = tcs;

        await _writer!.WriteLineAsync(request.ToString(Formatting.None));

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, _cts.Token);

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(-1, linkedCts.Token));

        if (completedTask == tcs.Task)
        {
            return await tcs.Task;
        }
        else
        {
            _pendingRequests.TryRemove(correlationId, out _);
            throw new TimeoutException("操作超时或被取消。");
        }
    }

    private JObject BuildStatusQuery(string targetClientId)
    {
        return new JObject
        {
            ["MessageType"] = "StatusQueryRequest",
            ["CorrelationId"] = Guid.NewGuid().ToString(),
            ["Payload"] = new JObject { ["TargetClientId"] = targetClientId }
        };
    }

    private JObject BuildCommandRequest(string targetClientId, string command)
    {
        return new JObject
        {
            ["MessageType"] = "CommandRequest",
            ["CorrelationId"] = Guid.NewGuid().ToString(),
            ["Payload"] = new JObject
            {
                ["TargetClientId"] = targetClientId,
                ["Command"] = command
            }
        };
    }

    public static async Task Main(string[] args)
    {
        var clientId = $"ControlClient-{Guid.NewGuid().ToString().Substring(0, 4)}";
        var client = new ControlClient(clientId, "127.0.0.1", 8888);
        await client.StartAsync();
    }
}