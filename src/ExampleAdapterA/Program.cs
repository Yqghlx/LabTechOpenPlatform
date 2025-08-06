using LabTech.GarnetAdapter;
using Microsoft.Extensions.Configuration;
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

            var logger = Log.ForContext<Program>();

            try
            {
                var garnetConnectionString = configuration["GarnetConnectionString"];
                var systemId = configuration["SystemId"];

                logger.Information("--- 示例适配器 A ---");

                var adapter = await GarnetAdapterClient.CreateAsync(garnetConnectionString, systemId, logger);

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
                    logger.Information("收到指令: {Message}", message);
                };

                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    logger.Information("正在关闭适配器...");
                    cts.Cancel();
                    e.Cancel = true;
                };

                await adapter.RunAsync(cts.Token);
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "应用程序启动失败");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
