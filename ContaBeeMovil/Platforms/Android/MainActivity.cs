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
            HandleIntent(Intent);

            // Si hay una recreación pendiente, ejecutarla UNA sola vez
            if (_pendingRecreate)
            {
                _pendingRecreate = false;
                Recreate();
            }
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            HandleIntent(intent);
        }

        private void HandleIntent(Intent? intent)
        {
            if (intent?.Action == Intent.ActionView && intent.Data != null)
            {
                var uri = intent.Data.ToString();
                DeepLinkHandler.HandleDeepLink(uri);
            }
        }
    }
}