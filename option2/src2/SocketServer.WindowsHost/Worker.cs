
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
        _socketServer.OnLogMessage += (message) => _logger.LogInformation(message);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _socketServer.StartAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Socket 服务器在运行时发生未处理的异常。");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Windows 服务正在停止。");
        _socketServer.Stop();
        await base.StopAsync(cancellationToken);
    }
}
