
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SocketServerLogic;

// This is a simplified version for recovery
public class SocketServer
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _serverCts = new CancellationTokenSource();

    public SocketServer(string ipAddress, int port)
    {
        _listener = new TcpListener(IPAddress.Parse(ipAddress), port);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        Console.WriteLine($"Server started on {_listener.LocalEndpoint}");
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_serverCts.Token, cancellationToken);
        try
        {
            while (!linkedCts.Token.IsCancellationRequested)
            {
                await _listener.AcceptTcpClientAsync(linkedCts.Token);
                // Simplified: No client handling in this recovery version
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Server stopping...");
        }
        finally
        {
            _listener.Stop();
            Console.WriteLine("Server stopped.");
        }
    }

    public void Stop()
    {
        if (!_serverCts.IsCancellationRequested)
        {
            _serverCts.Cancel();
        }
    }
}
