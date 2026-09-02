using banditoth.MAUI.DeviceId;
using CommunityToolkit.Maui;
using Contabee.Api;
using Contabee.Api.abstractions;
using Contabee.Pages.Registro;
using ContaBeeMovil.PageModels.Camara;
using ContaBeeMovil.Pages.Camara;
using ContaBeeMovil.Pages.Confirmar;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Pages.Captura;
using ContaBeeMovil.Pages.Perfil;
using ContaBeeMovil.Pages.Tienda;
using ContaBeeMovil.Pages.RecuperarPass;
using ContaBeeMovil.Pages.Sugerencias;
using ContaBeeMovil.Pages.Registro;
using ContaBeeMovil.Pages.Dashboard;
using ContaBeeMovil.Pages.Equipo;
using ContaBeeMovil.Pages.Devoluciones;
using ContaBeeMovil.Pages.Comprobaciones;
using ContaBeeMovil.Pages.SinConexion;
using ContaBeeMovil.Pages.AcercaDe;
using ContaBeeMovil.Services.Almacenamiento;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Camara;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Documento;
using ContaBeeMovil.Services.Horario;
using ContaBeeMovil.Services.Pdf;
using ContaBeeMovil.Services.Permisos;
using Plugin.Maui.OCR;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.IAP;
using ContaBeeMovil.Services.Notifications;
using ZXing.Net.Maui.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Handlers;
using Syncfusion.Maui.Toolkit.Hosting;
using SkiaSharp.Views.Maui.Controls.Hosting;
using ContaBee.Pages.Cupones;
using ContaBee.Services;
using ContaBee.Models.Configuracion;
using ContaBeeMovil.Pages.Dev;


namespace ContaBeeMovil
{
    public static class MauiProgram
    {
        public static IServiceProvider Services { get; private set; } = null!;

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder.UseMauiApp<App>();
            builder.UseSkiaSharp();
            builder.UseBarcodeReader();
            builder.UseMauiCommunityToolkit();
            builder.ConfigureSyncfusionToolkit();
            builder.ConfigureDeviceIdProvider();
            builder.UseOcr();
            builder.ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler<Shell, CustomShellRenderer>();
                handlers.AddHandler<Microsoft.Maui.Controls.DatePicker, ContaBeeDatePickerHandler>();
#endif
#if WINDOWS
                    Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
                    {
                        handler.PlatformView.SingleSelectionFollowsFocus = false;
                    });
#endif
#if IOS
                Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("BorderlessEntry", (handler, view) =>
                {
                    handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
                });
#endif
            });

            builder.ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                fonts.AddFont("FluentSystemIcons-Filled.ttf", Fonts.FluentUIFilled.FontFamily);
                fonts.AddFont("MaterialSymbols-Regular.ttf", Fonts.MaterialSymbols.FontFamily);
                fonts.AddFont("MaterialSymbols-Filled.ttf", Fonts.MaterialSymbolsFilled.FontFamily);
            });

#if DEBUG
            builder.Logging.AddDebug();
            builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            builder.Services.AddSingleton<IServicioToast, ServicioToast>();
            builder.Services.AddSingleton<IServicioAlmacenamiento, ServicioAlmacenamiento>();
            builder.Services.AddSingleton<DeviceService>();
            builder.Services.AddSingleton<IServicioSesion, ServicioSesion>();
            builder.Services.AddSingleton<ContaBeeMovil.Services.Sesion.ICoordinadorSesion, ContaBeeMovil.Services.Sesion.CoordinadorSesion>();
            builder.Services.AddSingleton<IServicioAlerta, ServicioAlerta>();
            builder.Services.AddSingleton(AppState.Instance);
            builder.Services.AddSingleton<IServicioPermisos, ServicioPermisos>();
            builder.Services.AddSingleton<IServicioCamara, ServicioCamara>();
            builder.Services.AddSingleton<IServicioIAP, ServicioIAP>();
            builder.Services.AddSingleton<IServicioReembolsoApple, ServicioReembolsoApple>();
            builder.Services.AddSingleton<IServicioReconciliacionIAP, ServicioReconciliacionIAP>();
            builder.Services.AddSingleton<IServicioLogs, ServicioLogs>();
            builder.Services.AddSingleton<IServicioModoDeveloper, ServicioModoDeveloper>();
            builder.Services.AddSingleton<IServicioSalud, ServicioSalud>();
            builder.Services.AddSingleton<IServicioActualizacion, ServicioActualizacion>();
            builder.Services.AddSingleton<IServicioProcesadorDocumento, ServicioProcesadorDocumento>();
            builder.Services.AddSingleton<IServicioRenderPdf, ServicioRenderPdf>();
            // Horario de captura delegada. Feriados desde GET /captura/diasinhabiles;
            // si el endpoint falla, el horario degrada a la regla de lunes a viernes.
            builder.Services.AddSingleton<IProveedorFeriados, ProveedorFeriadosApi>();
            builder.Services.AddSingleton<IServicioHorarioCaptura, ServicioHorarioCaptura>();
            builder.Services.AddSingleton<InterruptorApi>();
            builder.Services.AddTransient<AuthHandler>();


            var appConfig = ServicioConfiguracion.ObtieneConfiguracion(TipoConfiguracion.Produccion);

#if WINDOWS && DEBUG
    appConfig = ServicioConfiguracion.ObtieneConfiguracion(TipoConfiguracion.DebugLocal);
#endif


            // Cliente sin AuthHandler para el endpoint de refresh token
            builder.Services.AddHttpClient("IdentityToken", client =>
            {
                client.BaseAddress = new Uri(appConfig.UrlIdentityToken);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                // El default de HttpClient son 100 s: con el identity caído, la UI se quedaba
                // colgada minuto y medio antes de que el refresh siquiera fallara. 15 s es de
                // sobra para un token, y AuthHandler reintenta con backoff.
                client.Timeout = TimeSpan.FromSeconds(15);
            });

            builder.Services.AddHttpClient<IServicioIdentidad, ServicioIdentidad>(client =>
            {
                client.BaseAddress = new Uri(appConfig.UrlIdentity);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddHttpMessageHandler<AuthHandler>();
            builder.Services.AddHttpClient<IServicioCrm, ServicioCrm>(client =>
            {
                client.BaseAddress = new Uri(appConfig.UrlCrm);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddHttpMessageHandler<AuthHandler>();
            builder.Services.AddHttpClient<IServicioTranscript, ServicioTranscript>(client =>
            {
                client.BaseAddress = new Uri(appConfig.UrlTranscript);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddHttpMessageHandler<AuthHandler>();
            builder.Services.AddHttpClient<IServicioEcommerce, ServicioEcommerce>(client =>
            {
                client.BaseAddress = new Uri(appConfig.UrlEcommerce);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddHttpMessageHandler<AuthHandler>();



            // ViewModels
            builder.Services.AddTransient<DashboardViewModel>();
            builder.Services.AddTransient<PaginaCuponesViewModel>();
            builder.Services.AddTransient<EquipoViewModel>();
            builder.Services.AddTransient<VincularViewModel>();
            builder.Services.AddTransient<SolicitudTokenViewModel>();
            // Pages
            builder.Services.AddTransient<DashboardPage>();
            builder.Services.AddTransient<FacturacionPage>();
            builder.Services.AddTransient<PaginaDevoluciones>();
            builder.Services.AddTransient<PaginaComprobaciones>();
            builder.Services.AddTransient<DetalleDevolucionPage>();
            builder.Services.AddTransient<DetalleComprobacionPage>();
            builder.Services.AddTransient<EquipoPage>();
            builder.Services.AddTransient<VincularPage>();
            builder.Services.AddTransient<SolicitudTokenPage>();
            builder.Services.AddTransient<MainTabbedPage>();
            builder.Services.AddTransient<PaginaCupones>();
            builder.Services.AddTransient<RegistroViewModel>();
            builder.Services.AddTransient<PaginaRegistro>();

            builder.Services.AddTransient<ConfirmarCuentaPage>();
            builder.Services.AddTransient<PaginaLogin>();
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<AppShell>();
            builder.Services.AddTransient<RecuperarPassPage>();
            builder.Services.AddTransient<RestablecerContrasenaPage>();
            builder.Services.AddTransient<VincularCuentaPage>();
            builder.Services.AddTransient<TarjetasPage>();
            builder.Services.AddTransient<RFCsPage>();
            builder.Services.AddTransient<RegistrarRFCsPage>();
            builder.Services.AddTransient<ResumenLicenciaRFCPage>();
            builder.Services.AddTransient<CambiarContrasenaPage>();
            builder.Services.AddTransient<MiCuentaPage>();
            builder.Services.AddTransient<EliminarCuentaPage>();
            builder.Services.AddTransient<ManualRegistroPage>();
            builder.Services.AddTransient<SugerenciasPage>();
            builder.Services.AddTransient<AcercaDePage>();
            builder.Services.AddTransient<CambiarContrasenaPage>();
            builder.Services.AddTransient<PaginaCaptura>();
            builder.Services.AddTransient<VisorImagenPage>();
            builder.Services.AddTransient<TiendaPage>();
            builder.Services.AddTransient<LogsPage>();
            builder.Services.AddTransient<PaginaSinConexion>();
            // Cámara pages and view models
            builder.Services.AddTransient<TomarFotoPageModel>();
            builder.Services.AddTransient<TomarFotoPage>();
            builder.Services.AddTransient<QRPage>();

            var app = builder.Build();
            Services = app.Services;
            return app;
        }
    }
}
