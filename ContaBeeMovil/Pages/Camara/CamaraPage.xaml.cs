using ContaBeeMovil.PageModels.Camara;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Camara;
using Contabee.Api.Logging;
using CommunityToolkit.Mvvm.Input;

namespace ContaBeeMovil.Pages.Camara;

public partial class CamaraPage : ContentPage
{
    private readonly IServicioAlerta _servicioAlerta =
        MauiProgram.Services.GetRequiredService<IServicioAlerta>();
    private readonly IAppLogger _logger;

    public CamaraPage(CamaraPageModel pageModel, IAppLogger logger)
    {
        InitializeComponent();
        _logger = logger;
        BindingContext = pageModel;

        SelectorPersona.Elementos = new List<string> { "Física", "Moral" };
        SelectorPersona.IndiceSeleccionado = 0;
    }

    private async void BtnProcesar_Clicked(object? sender, EventArgs e)
    {
        try
        {
            var vm = BindingContext as CamaraPageModel;
            if (vm == null) return;

            var servicio = MauiProgram.Services.GetService(typeof(IServicioCamara)) as IServicioCamara;
            if (servicio == null)
            {
                _logger.Debug("Camara.ProcesarServicioNoDisponible", "Servicio de cámara no disponible para procesar imagen.");
                await _servicioAlerta.MostrarAsync("Error", "Servicio cámara no disponible.", verBotonCancelar: false, confirmarText: "OK");
                return;
            }

            _logger.Info("Camara.Procesar", "Inicio de procesamiento de imagen para detectar QR.");

            if (string.IsNullOrEmpty(vm.PhotoPath))
            {
                if (vm.TomarFotoCommand is IAsyncRelayCommand asyncCmd)
                    await asyncCmd.ExecuteAsync(null);
                else
                    vm.TomarFotoCommand?.Execute(null);
            }

            var qr = await servicio.ProcesarImagenAsync(vm.PhotoPath);
            if (string.IsNullOrEmpty(qr))
            {
                _logger.Debug("Camara.ProcesarQrNoDetectado", "No se detectó QR en la imagen procesada.");
                await _servicioAlerta.MostrarAsync("QR", "No se detectó QR en la imagen.", verBotonCancelar: false, confirmarText: "OK");
                return;
            }

            _logger.Info("Camara.ProcesarExitoso", "QR detectado correctamente en la imagen.");
            await _servicioAlerta.MostrarAsync("QR detectado", qr, verBotonCancelar: false, confirmarText: "OK");
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            _logger.Debug("Camara.ProcesarException", "Excepción no controlada al procesar imagen de cámara.", ex);
            await _servicioAlerta.MostrarAsync("Error", "Ocurrió un error al procesar la imagen.", verBotonCancelar: false, confirmarText: "OK");
        }
    }
}
