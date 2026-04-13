using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using StatusBar = CommunityToolkit.Maui.Core.Platform.StatusBar;
using ContaBeeMovil.Pages.Confirmar;
using ContaBeeMovil.Pages.Demo;
using ContaBeeMovil.Pages.Captura;
using ContaBeeMovil.Pages.Perfil;
using ContaBeeMovil.Pages.Registro;
using ContaBeeMovil.Pages.Dev;
using ContaBeeMovil.Pages.Sugerencias;
using ContaBeeMovil.Pages.Tienda;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
using Font = Microsoft.Maui.Font;

namespace ContaBeeMovil
{
    public partial class AppShell : Shell
    {
        private readonly IServicioSesion _servicioSesion;
        private readonly IServicioAlerta _servicioAlerta;

        public AppShell(IServicioSesion servicioSesion, IServicioAlerta servicioAlerta)
        {
            // ANTES de InitializeComponent para que DynamicResource
            // tenga el valor correcto desde el primer frame
            var isDarkInicial = Application.Current!.RequestedTheme == AppTheme.Dark;
            Application.Current.Resources["CurrentShellBarColor"] =
                isDarkInicial ? Color.FromArgb("#3a3a3a") : Color.FromArgb("#fefdfc");

            InitializeComponent();

            _servicioSesion = servicioSesion;
            _servicioAlerta = servicioAlerta;

            var currentTheme = Application.Current!.RequestedTheme;
            ThemeSwitch.IsToggled = currentTheme == AppTheme.Dark;
            RegisterRoutes();
            _ = CargarNombreUsuarioAsync();
            _ = servicioSesion.GetTarjetasAsync();

            ActualizarVisibilidadLogs();

            AppState.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppState.Perfil))
                    MainThread.BeginInvokeOnMainThread(ActualizarNombreLabel);
                else if (e.PropertyName == nameof(AppState.EsDev))
                    MainThread.BeginInvokeOnMainThread(ActualizarVisibilidadLogs);
            };

            Navigated += (_, _) => AplicarEstiloIconosConDelay();
            Loaded += (_, _) => AplicarEstiloIconosConDelay();

            this.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FlyoutIsPresented))
                    AplicarEstiloIconosConDelay();
            };

            Application.Current.RequestedThemeChanged += (_, e) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var isDark = e.RequestedTheme == AppTheme.Dark;

                    // Actualizar DynamicResource → Shell detecta el cambio
                    // → llama SetAppearance automáticamente con el color nuevo
                    Application.Current!.Resources["CurrentShellBarColor"] =
                        isDark ? Color.FromArgb("#3a3a3a") : Color.FromArgb("#fefdfc");

                    // iOS no usa Shell renderer, lo manejamos directo
#if IOS
                    ActualizarEstiloIconos();
#endif
                });
            };
        }

        private static void AplicarEstiloIconosConDelay()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                ActualizarEstiloIconos();
                await Task.Delay(50);
                ActualizarEstiloIconos();
            });
        }

        private static void ActualizarEstiloIconos()
        {
#if ANDROID
            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
            var color = isDark ? Color.FromArgb("#3a3a3a") : Color.FromArgb("#fefdfc");
            CustomToolbarAppearanceTracker.AplicarColorStatusBar(color);
#elif IOS
            if (!OperatingSystem.IsIOSVersionAtLeast(13))
                return;
            var isDarkIos = Application.Current!.RequestedTheme == AppTheme.Dark;
            var estilo = isDarkIos ? StatusBarStyle.LightContent : StatusBarStyle.DarkContent;
            var colorIos = isDarkIos ? Color.FromArgb("#3a3a3a") : Color.FromArgb("#fefdfc");
            StatusBar.SetColor(colorIos);
            StatusBar.SetStyle(estilo);
#endif
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

        private void ActualizarVisibilidadLogs()
        {
            if (this.FindByName<Grid>("LogsMenuItem") is { } item)
                item.IsVisible = AppState.Instance.EsDev;
        }

        private void ActualizarNombreLabel()
        {
            var nombre = AppState.Instance.Perfil?.DisplayName;
            if (string.IsNullOrWhiteSpace(nombre))
                nombre = _emailUsuario;
            if (!string.IsNullOrEmpty(nombre) && nombre.Contains('@'))
                nombre = nombre[..nombre.IndexOf('@')];
            if (!string.IsNullOrEmpty(nombre))
                LabelNombreUsuario.Text = nombre;
        }

        // ── Toggle de tema ────────────────────────────────────────────────

        private async void OnThemeSwitchToggled(object? sender, ToggledEventArgs e)
        {
            Application.Current!.UserAppTheme = e.Value ? AppTheme.Dark : AppTheme.Light;
            FlyoutIsPresented = false;

#if ANDROID

            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity?.Window != null)
            {
                // Estos flags son NECESARIOS para que SetStatusBarColor funcione
                activity.Window.AddFlags(
                    Android.Views.WindowManagerFlags.DrawsSystemBarBackgrounds);
                activity.Window.ClearFlags(
                    Android.Views.WindowManagerFlags.TranslucentStatus);
            }
#endif
        }
        // ── Cerrar sesión ─────────────────────────────────────────────────

        private async void OnCerrarSesionClicked(object? sender, EventArgs e)
        {
            bool confirmar = await _servicioAlerta.MostrarAsync(
                titulo: "Cerrar sesión",
                mensaje: "¿Estás seguro que deseas cerrar sesión?",
                cancelarText: "No",
                confirmarText: "Sí");
            if (!confirmar) return;
            await _servicioSesion.CerrarSesionAsync();
        }

        // ── Navegación ────────────────────────────────────────────────────

        private async void OnVincularmeClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await Shell.Current.GoToAsync(nameof(VincularCuentaPage));
        }

        private async void OnTiendaClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await Shell.Current.GoToAsync(nameof(TiendaPage));
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

        private async void OnLogsClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await GoToAsync(nameof(LogsPage));
        }

        private async void OnConfiguracionClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await Shell.Current.GoToAsync(nameof(ConfiguracionPage));
        }

        private async void OnDiagnosticoClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await _servicioAlerta.MostrarAsync("Diagnostico", "Próximamente",
                verBotonCancelar: false, confirmarText: "OK");
        }

        private async void OnBuzonClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await Shell.Current.GoToAsync(nameof(SugerenciasPage));
        }

        private async void OnAcercaDeClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            var version = AppInfo.VersionString;
            await _servicioAlerta.MostrarAsync("Acerca de", $"ContaBee — Versión {version}",
                verBotonCancelar: false, confirmarText: "OK");
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
            Routing.RegisterRoute(nameof(TiendaPage), typeof(TiendaPage));
            Routing.RegisterRoute(nameof(VincularCuentaPage), typeof(VincularCuentaPage));
            Routing.RegisterRoute(nameof(TarjetasPage), typeof(TarjetasPage));
            Routing.RegisterRoute(nameof(RFCsPage), typeof(RFCsPage));
            Routing.RegisterRoute(nameof(RegistrarRFCsPage), typeof(RegistrarRFCsPage));
            Routing.RegisterRoute(nameof(CambiarContrasenaPage), typeof(CambiarContrasenaPage));
            Routing.RegisterRoute(nameof(ConfiguracionPage), typeof(ConfiguracionPage));
            Routing.RegisterRoute(nameof(EliminarCuentaPage), typeof(EliminarCuentaPage));
            Routing.RegisterRoute(nameof(SugerenciasPage), typeof(SugerenciasPage));
            Routing.RegisterRoute(nameof(PaginaCaptura), typeof(PaginaCaptura));
            Routing.RegisterRoute(nameof(VisorImagenPage), typeof(VisorImagenPage));
            Routing.RegisterRoute(nameof(ReclamarDemoPage), typeof(ReclamarDemoPage));
            Routing.RegisterRoute(nameof(LogsPage), typeof(LogsPage));
        }
    }
}