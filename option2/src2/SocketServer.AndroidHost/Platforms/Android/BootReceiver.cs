using Android.App;
using Android.Content;
using System.Runtime.Versioning;

namespace SocketServer.AndroidHost.Platforms.Android
{
    [BroadcastReceiver(Enabled = true, Exported = true)]
    [IntentFilter(new[] { Intent.ActionBootCompleted })]
    public class BootReceiver : BroadcastReceiver
    {
        [SupportedOSPlatform("android26.0")]
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (context == null || intent == null) return;

            if (intent.Action == Intent.ActionBootCompleted)
            {
                var serviceIntent = new Intent(context, typeof(MyLongRunningService));
                context.StartForegroundService(serviceIntent);
            }
        }
    }
}