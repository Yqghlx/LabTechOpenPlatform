
using System.Runtime.Versioning;

namespace SocketServer.AndroidHost;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

    [SupportedOSPlatform("android26.0")]
    private void OnStartServiceClicked(object sender, EventArgs e)
    {
        #if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            if (Permissions.CheckStatusAsync<Permissions.PostNotifications>().Result != PermissionStatus.Granted)
            {
                Permissions.RequestAsync<Permissions.PostNotifications>();
            }
        }

        var intent = new Android.Content.Intent(Android.App.Application.Context, typeof(Platforms.Android.MyLongRunningService));
        Android.App.Application.Context.StartForegroundService(intent);
        DisplayAlert("服务", "服务启动命令已发送。", "好的");
        #endif
    }

    private void OnStopServiceClicked(object sender, EventArgs e)
    {
        #if ANDROID
        var intent = new Android.Content.Intent(Android.App.Application.Context, typeof(Platforms.Android.MyLongRunningService));
        Android.App.Application.Context.StopService(intent);
        DisplayAlert("服务", "服务停止命令已发送。", "好的");
        #endif
    }

    [SupportedOSPlatform("android23.0")]
    private void OnRequestIgnoreBatteryOptimizationsClicked(object sender, EventArgs e)
    {
        #if ANDROID
        var pm = (Android.OS.PowerManager)Android.App.Application.Context.GetSystemService(Android.Content.Context.PowerService)!;
        if (pm == null) return;

        string packageName = Android.App.Application.Context.PackageName ?? string.Empty;

        if (string.IsNullOrEmpty(packageName)) return;

        if (pm.IsIgnoringBatteryOptimizations(packageName))
        {
            DisplayAlert("权限", "应用已经处于电池优化白名单中。", "好的");
            return;
        }

        var intent = new Android.Content.Intent(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
        intent.SetData(Android.Net.Uri.Parse("package:" + packageName));
        intent.AddFlags(Android.Content.ActivityFlags.NewTask);
        Android.App.Application.Context.StartActivity(intent);
        #endif
    }
}
