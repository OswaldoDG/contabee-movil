using banditoth.MAUI.DeviceId;
using CommunityToolkit.Maui;
using ZXing.Net.Maui.Controls;
using Contabee.Api;
using Contabee.Api.abstractions;
using Contabee.Api.Identidad;
using Contabee.Pages.Registro;
using ContaBeeMovil.PageModels.Camara;
using ContaBeeMovil.Pages.Camara;
using ContaBeeMovil.Pages.Confirmar;
using ContaBeeMovil.Pages.Demo;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Pages.Captura;
using ContaBeeMovil.Pages.Perfil;
using ContaBeeMovil.Pages.Tienda;
using ContaBeeMovil.Pages.RecuperarPass;
using ContaBeeMovil.Pages.Sugerencias;
using ContaBeeMovil.Pages.Registro;
using ContaBeeMovil.Pages.Dashboard;
using ContaBeeMovil.Services.Almacenamiento;
using ContaBeeMovil.Services.Camara;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Notifications;
using MauiIcons.Material;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Handlers;
using Syncfusion.Maui.Toolkit.Hosting;


namespace ContaBeeMovil
{
    public static class MauiProgram
    {
        public static IServiceProvider Services { get; private set; } = null!;

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMaterialMauiIcons()
                .UseMauiCommunityToolkit()
                .UseBarcodeReader()
                .ConfigureSyncfusionToolkit()
                .ConfigureDeviceIdProvider()
                .ConfigureMauiHandlers(handlers =>
                {
#if WINDOWS
                    Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
                    {
                        handler.PlatformView.SingleSelectionFollowsFocus = false;
                    });
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

#if DEBUG
            builder.Logging.AddDebug();
            builder.Services.AddLogging(configure => configure.AddDebug());
#endif
            //Servicioos

            builder.Services.AddSingleton<IToastService, ToastService>();
            builder.Services.AddSingleton<IServicioAlmacenamiento, ServicioAlmacenamiento>();
            builder.Services.AddSingleton<DeviceService>();
            builder.Services.AddSingleton<IServicioSesion, ServicioSesion>();
            builder.Services.AddSingleton<IServicioNotificacion, ServicioNotificacion>();
            builder.Services.AddSingleton<IServicioAlerta, ServicioAlerta>();
            builder.Services.AddSingleton(AppState.Instance);
            builder.Services.AddSingleton<IServicioCamara, ServicioCamara>();
            builder.Services.AddTransient<AuthHandler>();

            // Cliente sin AuthHandler para el endpoint de refresh token
            builder.Services.AddHttpClient("IdentityToken", client =>
            {
                client.BaseAddress = new Uri("https://api.contabee.mx/api/identity/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            builder.Services.AddHttpClient<IServicioIdentidad, ServicioIdentidad>(client =>
            {
                client.BaseAddress = new Uri("https://api.contabee.mx/api/identity/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddHttpMessageHandler<AuthHandler>();
            builder.Services.AddHttpClient<IServicioCrm, ServicioCrm>(client =>
            {
                client.BaseAddress = new Uri("https://api.contabee.mx/api/crm/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddHttpMessageHandler<AuthHandler>();
            builder.Services.AddHttpClient<IServicioTranscript, ServicioTranscript>(client =>
            {
                client.BaseAddress = new Uri("https://api.contabee.mx/api/transcript/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddHttpMessageHandler<AuthHandler>();





            // ViewModels
            builder.Services.AddTransient<DashboardViewModel>();

            //paginas
            builder.Services.AddSingleton<ProjectRepository>();
            builder.Services.AddSingleton<TaskRepository>();
            builder.Services.AddSingleton<CategoryRepository>();
            builder.Services.AddSingleton<TagRepository>();
            builder.Services.AddSingleton<SeedDataService>();
            builder.Services.AddSingleton<ModalErrorHandler>();
            builder.Services.AddTransient<DashboardPage>();
            builder.Services.AddTransient<FacturacionPage>();
            builder.Services.AddTransient<EquipoPage>();

            builder.Services.AddTransient<RegistroViewModel>();
            builder.Services.AddTransient<PaginaRegistro>();
            // Registro pages

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
            builder.Services.AddTransient<CambiarContrasenaPage>();
            builder.Services.AddTransient<ManualRegistroPage>();
            builder.Services.AddTransient<SugerenciasPage>();
            builder.Services.AddTransient<CambiarContrasenaPage>();
            builder.Services.AddTransient<PaginaCaptura>();
            builder.Services.AddTransient<TiendaPage>();
            builder.Services.AddTransient<ReclamarDemoPage>();
            // Cámara pages and view models
            builder.Services.AddTransient<TomarFotoPageModel>();
            builder.Services.AddTransient<TomarFotoPage>();
            builder.Services.AddTransient<QRPageModel>();
            builder.Services.AddTransient<QRPage>();




            var app = builder.Build();
            Services = app.Services;
            return app;






        }
    }
}
