
using Android.App;
using Android.Content;

namespace SocketServer.AndroidHost.Platforms.Android
{
    [BroadcastReceiver(Enabled = true, Exported = true)]
    [IntentFilter(new[] { Intent.ActionBootCompleted })]
    public class BootReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action == Intent.ActionBootCompleted)
            {
                var serviceIntent = new Intent(context, typeof(MyLongRunningService));
                context.StartForegroundService(serviceIntent);
            }
        }
    }
}
