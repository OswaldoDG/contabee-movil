using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services.Dev;

namespace ContaBeeMovil
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTask,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                               ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter(new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "contabee",
        DataHost = "contabee.app.link",
        AutoVerify = true)]
    [IntentFilter(new[] { Intent.ActionSend },
        Categories = new[] { Intent.CategoryDefault },
        DataMimeType = "image/*",
        Label = "Compartir a ContaBee")]
    public class MainActivity : MauiAppCompatActivity
    {
        private static bool _pendingRecreate = false;
        private static bool _pendingWidgetNavigation = false;

        public static void SolicitarRecreacion()
        {
            _pendingRecreate = true;
        }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            HandleIntent(Intent, desdeOnCreate: true);

            if (_pendingRecreate)
            {
                _pendingRecreate = false;
                Recreate();
            }
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            HandleIntent(intent, desdeOnCreate: false);
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (_pendingWidgetNavigation)
            {
                _pendingWidgetNavigation = false;
                DeepLinkHandler.HandleWidgetCaptura();
            }
        }

        private void HandleIntent(Intent? intent, bool desdeOnCreate)
        {
            if (intent?.GetBooleanExtra(WidgetCaptura.ExtraWidgetCaptura, false) == true)
            {
                // Consumir el extra: si la Activity se recrea (p. ej. Recreate por cambio
                // de tema) OnCreate reutiliza este mismo Intent y volvería a dispararse.
                intent.RemoveExtra(WidgetCaptura.ExtraWidgetCaptura);

                // La re-entrega del intent del widget desde Recientes SOLO puede ocurrir en
                // OnCreate (recreación tras muerte del proceso); ahí el usuario solo quiere
                // abrir la app. Un intent que llega por OnNewIntent es siempre un tap
                // legítimo — algunos launchers marcan LaunchedFromHistory también en esos
                // taps, así que el filtro no debe aplicarse en esa ruta.
                bool redeliveryDesdeRecientes = desdeOnCreate &&
                    (intent.Flags & ActivityFlags.LaunchedFromHistory) != 0;

                if (!redeliveryDesdeRecientes)
                    _pendingWidgetNavigation = true;
                return;
            }

            if (intent?.Action == Intent.ActionView && intent.Data != null)
            {
                var uri = intent.Data.ToString();
                DeepLinkHandler.HandleDeepLink(uri);
                return;
            }

            if (intent?.Action == Intent.ActionSend && intent.Type?.StartsWith("image/") == true)
                HandleShareIntent(intent);
        }

        private void HandleShareIntent(Intent intent)
        {
#pragma warning disable CA1422
            var uri = intent.GetParcelableExtra(Intent.ExtraStream) as Android.Net.Uri;
#pragma warning restore CA1422
            if (uri == null) return;

            try
            {
                var fileName = $"shared_{DateTime.Now:yyyyMMddHHmmss}.jpg";
                var destPath = System.IO.Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, fileName);

                using var inputStream = ContentResolver!.OpenInputStream(uri);
                using var outputStream = System.IO.File.OpenWrite(destPath);
                inputStream!.CopyTo(outputStream);

                SharedImageHandler.HandleSharedImage(fileName);
            }
            catch (Exception ex)
            {
                App.Services?.GetService<IServicioLogs>()?.Log($"[SharedImage] Android: error copiando imagen — {ex.Message}");
            }
        }
    }
}
