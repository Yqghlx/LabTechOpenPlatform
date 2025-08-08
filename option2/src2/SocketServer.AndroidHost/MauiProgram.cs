using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace SocketServer.AndroidHost;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
        // 配置 Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
                        .WriteTo.AndroidLog()
            .CreateLogger();

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

        // 添加 Serilog 作为日志记录提供程序
        builder.Logging.AddSerilog(dispose: true);

        builder.Services.AddSingleton<MainPage>();

		return builder.Build();
	}
}