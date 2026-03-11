using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using ContaBeeMovil.Pages.Confirmar;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Pages.Perfil;
using ContaBeeMovil.Pages.Registro;
using Font = Microsoft.Maui.Font;

namespace ContaBeeMovil
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            var currentTheme = Application.Current!.RequestedTheme;
            ThemeSwitch.IsToggled = currentTheme == AppTheme.Dark;
            RegisterRoutes();
            _ = CargarEmailUsuarioAsync();
        }

        // ── Snackbar / Toast helpers ──────────────────────────────────────

        public static async Task DisplaySnackbarAsync(string message)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            var snackbarOptions = new SnackbarOptions
            {
                BackgroundColor = Color.FromArgb("#FF3300"),
                TextColor = Colors.White,
                ActionButtonTextColor = Colors.Yellow,
                CornerRadius = new CornerRadius(0),
                Font = Font.SystemFontOfSize(18),
                ActionButtonFont = Font.SystemFontOfSize(14)
            };

            var snackbar = Snackbar.Make(message, visualOptions: snackbarOptions);
            await snackbar.Show(cancellationTokenSource.Token);
        }

        public static async Task DisplayToastAsync(string message)
        {
            if (OperatingSystem.IsWindows())
                return;

            var toast = Toast.Make(message, textSize: 18);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await toast.Show(cts.Token);
        }

        // ── Header: cargar email del usuario ─────────────────────────────

        private async Task CargarEmailUsuarioAsync()
        {
            var svc = MauiProgram.Services.GetRequiredService<IServicioSesion>();
            var email = await svc.LeeEmailAsync();
            if (!string.IsNullOrEmpty(email))
                LabelNombreUsuario.Text = email;
        }

        // ── Toggle de tema ────────────────────────────────────────────────

        private void OnThemeSwitchToggled(object? sender, ToggledEventArgs e)
            => Application.Current!.UserAppTheme = e.Value ? AppTheme.Dark : AppTheme.Light;

        // ── Cerrar sesión ─────────────────────────────────────────────────

        private async void OnCerrarSesionClicked(object? sender, EventArgs e)
        {
            bool confirmar = await DisplayAlert(
                "Cerrar sesión",
                "¿Estás seguro que deseas cerrar sesión?",
                "Sí", "No");

            if (!confirmar)
                return;

            var servicioSesion = MauiProgram.Services.GetRequiredService<IServicioSesion>();
            await servicioSesion.LimpiaTokensAsync();

            var paginaLogin = MauiProgram.Services.GetRequiredService<PaginaLogin>();
            Application.Current!.Windows[0].Page = paginaLogin;
        }

        // ── Navegación: items card ────────────────────────────────────────

        private async void OnVincularmeClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await Shell.Current.GoToAsync(nameof(VincularCuentaPage));
        }

        private async void OnTarjetasClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await Shell.Current.GoToAsync(nameof(TarjetasPage));
        }

        private async void OnRFCsClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await Shell.Current.GoToAsync(nameof(RFCsPage));
        }

        private async void OnContrasenaClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await Shell.Current.GoToAsync(nameof(CambiarContrasenaPage));
        }

        // ── Items simples ─────────────────────────────────────────────────

        private async void OnConfiguracionClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await DisplayAlert("Configuración", "Próximamente", "OK");
        }

        private async void OnDiagnosticoClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await DisplayAlert("Diagnostico", "Próximamente", "OK");
        }

        private async void OnBuzonClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await DisplayAlert("Búzon de sugerencias", "Próximamente", "OK");
        }

        private async void OnAcercaDeClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await DisplayAlert("Acerca de", "ContaBee — Versión 1.0", "OK");
        }

        private async void OnCompartirClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "Compartir ContaBee",
                Text = "Descarga ContaBee: https://contabee.mx"
            });
        }

        // ── Registro de rutas ─────────────────────────────────────────────

        void RegisterRoutes()
        {
            Routing.RegisterRoute("cuenta/confirmar", typeof(ConfirmarCuentaPage));
            Routing.RegisterRoute(nameof(PaginaRegistro), typeof(PaginaRegistro));
            Routing.RegisterRoute(nameof(VincularCuentaPage), typeof(VincularCuentaPage));
            Routing.RegisterRoute(nameof(TarjetasPage), typeof(TarjetasPage));
            Routing.RegisterRoute(nameof(RFCsPage), typeof(RFCsPage));
            Routing.RegisterRoute(nameof(CambiarContrasenaPage), typeof(CambiarContrasenaPage));
        }
    }
}
