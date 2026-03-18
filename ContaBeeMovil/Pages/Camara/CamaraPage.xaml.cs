using ContaBeeMovil.PageModels.Camara;
using ContaBeeMovil.Services.Camara;
using CommunityToolkit.Mvvm.Input;

namespace ContaBeeMovil.Pages.Camara;

public partial class CamaraPage : ContentPage
{
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
            await DisplayAlert("Error", "Servicio cámara no disponible.", "OK");
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
            await DisplayAlert("QR", "No se detectó QR en la imagen.", "OK");
            return;
        }

        await DisplayAlert("QR detectado", qr, "OK");
        await Navigation.PopModalAsync();
    }
}
