using LabTech.GarnetAdapter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ExampleAdapterA
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .CreateLogger();

            var serilogLogger = Log.ForContext<Program>();

            try
            {
                var garnetConnectionString = configuration["GarnetConnectionString"];
                var systemId = configuration["SystemId"];

                if (string.IsNullOrEmpty(garnetConnectionString) || string.IsNullOrEmpty(systemId))
                {
                    serilogLogger.Fatal("配置缺失: 必须在 appsettings.json 中提供 GarnetConnectionString 和 SystemId。");
                    throw new InvalidOperationException("必要的配置缺失，程序无法启动。");
                }

                var msLogger = new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger).CreateLogger(typeof(Program).FullName!);

                serilogLogger.Information("--- 示例适配器 A ---");

                var adapter = await GarnetAdapterClient.CreateAsync(garnetConnectionString, systemId, msLogger);

                var updateInterval = configuration.GetValue<int?>("StateUpdateInterval");
                if (updateInterval.HasValue)
                {
                    adapter.StateUpdateInterval = updateInterval.Value;
                }

                adapter.StateGenerator = () =>
                {
                    var random = new Random();
                    return new
                    {
                        SystemId = systemId,
                        Timestamp = DateTime.UtcNow.ToString("o"),
                        Status = "online",
                        Metrics = new
                        {
                            CpuUsage = $"{random.NextDouble() * 100:F2}%",
                            MemoryUsage = $"{random.Next(500, 4096)}MB"
                        }
                    };
                };

                adapter.OnCommandReceived = (message) =>
                {
                    serilogLogger.Information("收到指令: {Message}", message);
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