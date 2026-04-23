using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using ContaBeeMovil.Helpers;

namespace ContaBeeMovil
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
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
        // Flag estático para evitar bucle infinito de recreaciones
        private static bool _pendingRecreate = false;

        public static void SolicitarRecreacion()
        {
            _pendingRecreate = true;
        }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // App estaba CERRADA y se abrió por el link
            HandleIntent(Intent);

            // Si hay una recreación
            // , ejecutarla UNA sola vez
            if (_pendingRecreate)
            {
                _pendingRecreate = false;
                Recreate();
            }
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);

            // App estaba ABIERTA en segundo plano
            HandleIntent(intent);
        }

        private void HandleIntent(Intent? intent)
        {
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

                System.Diagnostics.Debug.WriteLine($"📷 Android share: imagen copiada a {destPath}");
                SharedImageHandler.HandleSharedImage(fileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Android share: error — {ex.Message}");
            }
        }
    }
}
