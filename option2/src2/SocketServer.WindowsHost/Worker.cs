
using SocketServerLogic;

namespace SocketServer.WindowsHost;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly SocketServerLogic.SocketServer _socketServer;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _socketServer = new SocketServerLogic.SocketServer("0.0.0.0", 8888);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _socketServer.StartAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Server failed to run.");
        }
    }
}
