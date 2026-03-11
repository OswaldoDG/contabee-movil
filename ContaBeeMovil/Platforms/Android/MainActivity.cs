using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using ContaBeeMovil.Helpers;

namespace ContaBeeMovil
{

    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]

    [IntentFilter(new[] { Intent.ActionView },
              Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "contabee",
    DataHost = "contabee.app.link",
    AutoVerify = true)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // App estaba CERRADA y se abrió por el link
            HandleIntent(Intent);
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
                DeepLinkHandler.HandleDeepLink(uri);  // ← Cambia esto
            }
        }
    }
}
