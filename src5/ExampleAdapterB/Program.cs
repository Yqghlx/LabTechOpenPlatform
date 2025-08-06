using LabTech.GarnetAdapter;
using Serilog;
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
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.AppSettings()
                .Enrich.FromLogContext()
                .CreateLogger();

            var logger = Log.ForContext<Program>();

            try
            {
                var garnetConnectionString = ConfigurationManager.AppSettings["GarnetConnectionString"];
                var systemId = ConfigurationManager.AppSettings["SystemId"];

                logger.Information("--- 示例适配器 B ---");

                var adapter = await GarnetAdapterClient.CreateAsync(garnetConnectionString, systemId, logger);

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
                    logger.Warning("执行了重启指令: {Message}", message);
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