using LabTech.GarnetAdapter;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace ExampleAdapterB
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // --- 通过代码直接配置 Serilog，不再依赖 App.config 的 serilog: 键 ---
            var logPath = ConfigurationManager.AppSettings["LogPath"] ?? "logs/adapter-b-.log";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .CreateLogger();

            var serilogLogger = Log.ForContext<Program>();

            try
            {
                var garnetConnectionString = ConfigurationManager.AppSettings["GarnetConnectionString"];
                var systemId = ConfigurationManager.AppSettings["SystemId"];

                if (string.IsNullOrEmpty(garnetConnectionString) || string.IsNullOrEmpty(systemId))
                {
                    serilogLogger.Fatal("配置缺失: 必须在 App.config 中提供 GarnetConnectionString 和 SystemId。");
                    throw new InvalidOperationException("必要的配置缺失，程序无法启动。");
                }

                var msLogger = new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger).CreateLogger(typeof(Program).FullName!);

                serilogLogger.Information("--- 示例适配器 B ---");

                var adapter = await GarnetAdapterClient.CreateAsync(garnetConnectionString, systemId, msLogger);

                adapter.StateGenerator = () =>
                {
                    var random = new Random();
                    return new
                    {
                        SystemId = systemId,
                        Timestamp = DateTime.UtcNow.ToString("o"),
                        Status = "healthy",
                        Data = new
                        {
                            Temperature = random.Next(20, 35),
                            Humidity = $"{random.Next(40, 60)}%"
                        }
                    };
                };

                adapter.OnCommandReceived = (message) =>
                {
                    serilogLogger.Warning("执行了重启指令: {Message}", message);
                };

                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    serilogLogger.Information("正在关闭适配器...");
                    cts.Cancel();
                    e.Cancel = true;
                };

                await adapter.RunAsync(cts.Token);
            }
            catch (Exception ex)
            {
                serilogLogger.Fatal(ex, "应用程序启动失败");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}