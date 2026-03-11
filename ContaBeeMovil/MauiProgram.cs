using banditoth.MAUI.DeviceId;
using CommunityToolkit.Maui;
using Contabee.Api;
using Contabee.Api.abstractions;
using Contabee.Api.Identidad;
using Contabee.Pages.Registro;
using ContaBeeMovil.Pages.Confirmar;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Pages.Registro;
using ContaBeeMovil.Services.Almacenamiento;
using ContaBeeMovil.Services.Device;
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
            builder.Services.AddSingleton<AppState>();
            builder.Services.AddTransient<AuthHandler>();

            builder.Services.AddHttpClient<IServicioIdentidad, ServicioIdentidad>(client =>
            {
                client.BaseAddress = new Uri("https://api.contabee.mx/api/identity");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddHttpMessageHandler<AuthHandler>(); 
            builder.Services.AddHttpClient<IServicioCrm, ServicioCrm>(client =>
            {
                client.BaseAddress = new Uri("https://api.contabee.mx/api/crm");
                client.DefaultRequestHeaders.Add("Accet", "application/json");
            }).AddHttpMessageHandler<AuthHandler>(); 
            builder.Services.AddHttpClient<IServicioTranscript, ServicioTranscript>(client =>
            {
                client.BaseAddress = new Uri("https://api.contabee.mx/api/transcript");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddHttpMessageHandler<AuthHandler>(); 

            
                


            //paginas
            builder.Services.AddSingleton<ProjectRepository>();
            builder.Services.AddSingleton<TaskRepository>();
            builder.Services.AddSingleton<CategoryRepository>();
            builder.Services.AddSingleton<TagRepository>();
            builder.Services.AddSingleton<SeedDataService>();
            builder.Services.AddSingleton<ModalErrorHandler>();
            builder.Services.AddTransient<Pages.DashboardPage>();
            builder.Services.AddTransient<Pages.FacturacionPage>();
            builder.Services.AddTransient<Pages.EquipoPage>();


            builder.Services.AddTransient<RegistroViewModel>();
            builder.Services.AddTransient<PaginaRegistro>();
            builder.Services.AddTransient<ConfirmarCuentaPage>();
            builder.Services.AddTransient<PaginaLogin>();
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<AppShell>();
            builder.Services.AddTransient<Pages.RecuperarPass.RecuperarPassPage>();
            builder.Services.AddTransient<Pages.Perfil.VincularCuentaPage>();
            builder.Services.AddTransient<Pages.Perfil.TarjetasPage>();
            builder.Services.AddTransient<Pages.Perfil.RFCsPage>();
            builder.Services.AddTransient<Pages.Perfil.CambiarContrasenaPage>();




            var app = builder.Build();
            Services = app.Services;
            return app;






        }
    }
}
