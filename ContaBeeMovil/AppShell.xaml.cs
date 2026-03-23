using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using ContaBeeMovil.Pages.Confirmar;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Pages.Captura;
using ContaBeeMovil.Pages.Perfil;
using ContaBeeMovil.Pages.Registro;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
using Font = Microsoft.Maui.Font;

namespace ContaBeeMovil
{
    public partial class AppShell : Shell
    {
        public AppShell(IServicioSesion servicioSesion, IServicioAlerta servicioAlerta)
        {
            InitializeComponent();
            _servicioSesion = servicioSesion;
            _servicioAlerta = servicioAlerta;
            var currentTheme = Application.Current!.RequestedTheme;
            ThemeSwitch.IsToggled = currentTheme == AppTheme.Dark;
            RegisterRoutes();
            _ = CargarNombreUsuarioAsync();

            AppState.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppState.Perfil))
                    MainThread.BeginInvokeOnMainThread(ActualizarNombreLabel);
            };

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

        // ── Header: nombre de usuario ─────────────────────────────────────

        private async Task CargarNombreUsuarioAsync()
        {
            _emailUsuario = AppState.Instance.Perfil?.DisplayName;
            ActualizarNombreLabel();
        }

        private string? _emailUsuario;
        private readonly IServicioSesion _servicioSesion;
        private readonly IServicioAlerta _servicioAlerta;

        private void ActualizarNombreLabel()
        {
            var nombre = AppState.Instance.Perfil?.DisplayName;
            if (string.IsNullOrWhiteSpace(nombre))
                nombre = _emailUsuario;

            if (!string.IsNullOrEmpty(nombre) && nombre.Contains('@'))
                nombre = nombre[..nombre.IndexOf('@')];

            if (!string.IsNullOrEmpty(nombre))
                LabelNombreUsuario.Text=nombre;
        }

        // ── Toggle de tema ────────────────────────────────────────────────

        private void OnThemeSwitchToggled(object? sender, ToggledEventArgs e)
            => Application.Current!.UserAppTheme = e.Value ? AppTheme.Dark : AppTheme.Light;

        // ── Cerrar sesión ─────────────────────────────────────────────────

        private async void OnCerrarSesionClicked(object? sender, EventArgs e)
        {
            bool confirmar = await _servicioAlerta.MostrarAsync(
                titulo: "Cerrar sesión",
                mensaje: "¿Estás seguro que deseas cerrar sesión?",
                cancelarText: "No",
                confirmarText: "Sí");

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
            await _servicioAlerta.MostrarAsync("Configuración", "Próximamente", verBotonCancelar: false, confirmarText: "OK");
        }

        private async void OnDiagnosticoClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await _servicioAlerta.MostrarAsync("Diagnostico", "Próximamente", verBotonCancelar: false, confirmarText: "OK");
        }

        private async void OnBuzonClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await _servicioAlerta.MostrarAsync("Búzon de sugerencias", "Próximamente", verBotonCancelar: false, confirmarText: "OK");
        }

        private async void OnAcercaDeClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await _servicioAlerta.MostrarAsync("Acerca de", "ContaBee — Versión 1.0", verBotonCancelar: false, confirmarText: "OK");
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
            Routing.RegisterRoute(nameof(RegistrarRFCsPage), typeof(RegistrarRFCsPage));
            Routing.RegisterRoute(nameof(CambiarContrasenaPage), typeof(CambiarContrasenaPage));
            Routing.RegisterRoute(nameof(PaginaCaptura), typeof(PaginaCaptura));
        }
    }
}
