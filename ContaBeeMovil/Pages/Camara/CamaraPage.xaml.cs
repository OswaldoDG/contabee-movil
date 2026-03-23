using ContaBeeMovil.PageModels.Camara;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Camara;
using CommunityToolkit.Mvvm.Input;

namespace ContaBeeMovil.Pages.Camara;

public partial class CamaraPage : ContentPage
{
    private readonly IServicioAlerta _servicioAlerta =
        MauiProgram.Services.GetRequiredService<IServicioAlerta>();

    public CamaraPage(CamaraPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;

        PickerPersona.Items.Add("Física");
        PickerPersona.Items.Add("Moral");
        PickerPersona.SelectedIndex = 0;
    }

    private async void BtnProcesar_Clicked(object? sender, EventArgs e)
    {
        var vm = BindingContext as CamaraPageModel;
        if (vm == null) return;

        var servicio = MauiProgram.Services.GetService(typeof(IServicioCamara)) as IServicioCamara;
        if (servicio == null)
        {
            await _servicioAlerta.MostrarAsync("Error", "Servicio cámara no disponible.", verBotonCancelar: false, confirmarText: "OK");
            return;
        }

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
            await _servicioAlerta.MostrarAsync("QR", "No se detectó QR en la imagen.", verBotonCancelar: false, confirmarText: "OK");
            return;
        }

        await _servicioAlerta.MostrarAsync("QR detectado", qr, verBotonCancelar: false, confirmarText: "OK");
        await Navigation.PopModalAsync();
    }
}
