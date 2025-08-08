
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using SocketServerLogic;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System;

namespace SocketServer.AndroidHost.Platforms.Android
{
    [Service(Name = "com.companyname.socketserver.androidhost.MyLongRunningService",
             ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
    public class MyLongRunningService : Service
    {
        private const int NOTIFICATION_ID = 1001;
        private const string NOTIFICATION_CHANNEL_ID = "MyLongRunningServiceChannel";
        
        private SocketServerLogic.SocketServer? _socketServer;
        private CancellationTokenSource? _cts;

        public override IBinder? OnBind(Intent? intent) => null;

        [SupportedOSPlatform("android26.0")]
        public override void OnCreate()
        {
            base.OnCreate();
            CreateNotificationChannel();
        }

        [SupportedOSPlatform("android29.0")]
        public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
        {
            var notification = CreateNotification("服务正在后台运行");
            StartForeground(NOTIFICATION_ID, notification, global::Android.Content.PM.ForegroundService.TypeDataSync);

            if (_cts == null || _cts.IsCancellationRequested)
            {
                _cts = new CancellationTokenSource();
                _socketServer = new SocketServerLogic.SocketServer("0.0.0.0", 8888);
                _socketServer.OnLogMessage += (message) => System.Diagnostics.Debug.WriteLine($"[SocketServer] {message}");
                Task.Run(() => _socketServer.StartAsync(_cts.Token));
            }

            return StartCommandResult.Sticky;
        }

        public override void OnDestroy()
        {
            _cts?.Cancel();
            _socketServer?.Stop();
            base.OnDestroy();
        }

        [SupportedOSPlatform("android26.0")]
        private void CreateNotificationChannel()
        {
            var channel = new NotificationChannel(NOTIFICATION_CHANNEL_ID, "后台服务", NotificationImportance.Default);
            if (GetSystemService(NotificationService) is NotificationManager manager)
            {
                manager.CreateNotificationChannel(channel);
            }
        }

        [SupportedOSPlatform("android23.0")]
        private Notification CreateNotification(string contentText)
        {
            var mainActivityIntent = new Intent(this, typeof(MainActivity));
            var pendingIntent = PendingIntent.GetActivity(this, 0, mainActivityIntent, PendingIntentFlags.Immutable);

            return new NotificationCompat.Builder(this, NOTIFICATION_CHANNEL_ID)
                .SetContentTitle("Socket 服务器")
                .SetContentText(contentText)
                .SetSmallIcon(Resource.Mipmap.appicon) 
                .SetContentIntent(pendingIntent)
                .SetOngoing(true)
                .Build();
        }
    }
}
