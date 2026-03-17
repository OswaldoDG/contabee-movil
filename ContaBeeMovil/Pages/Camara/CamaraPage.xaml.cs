using ContaBeeMovil.Helpers;
using ContaBeeMovil.PageModels.Camara;
using ContaBeeMovil.Services.Camara;
using CommunityToolkit.Mvvm.Input;
using System.Linq;

namespace ContaBeeMovil.Pages.Camara;

public partial class CamaraPage : ContentPage
{
    public CamaraPage(CamaraPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;

        try
        {
            this.BackgroundColor = UIHelpers.GetColor("Background");
            LabelTipoPersona.TextColor = UIHelpers.GetColor("PrimaryText");
            LabelCompartido.TextColor = UIHelpers.GetColor("PrimaryText");
            LabelHint.TextColor = UIHelpers.GetColor("SecondaryText");
        }
        catch { }

        // Populate picker
        PickerPersona.Items.Add("Física");
        PickerPersona.Items.Add("Moral");
        PickerPersona.SelectedIndex = 0;

        BtnProcesar.Clicked += async (_, __) =>
        {
            var vm = BindingContext as CamaraPageModel;
            if (vm == null)
                return;

            // call ServicioCamara to process QR from the last photo path
            var servicio = MauiProgram.Services.GetService(typeof(IServicioCamara)) as IServicioCamara;
            if (servicio == null)
            {
                await DisplayAlert("Error", "Servicio cámara no disponible.", "OK");
                return;
            }

            // Si no hay foto tomada, pide tomar una
            if (string.IsNullOrEmpty(vm.PhotoPath))
            {
                if (vm.TomarFotoCommand is IAsyncRelayCommand asyncCmd)
                {
                    await asyncCmd.ExecuteAsync(null);
                }
                else
                {
                    // fallback: try execute command
                    vm.TomarFotoCommand?.Execute(null);
                }
            }

            // Procesar la foto para extraer QR usando el servicio (procesar la imagen tomada)
            var qr = await servicio.ProcesarImagenAsync(vm.PhotoPath);
            if (string.IsNullOrEmpty(qr))
            {
                await DisplayAlert("QR", "No se detectó QR en la imagen.", "OK");
                return;
            }

            // Mostrar y cerrar
            await DisplayAlert("QR detectado", qr, "OK");
            await Navigation.PopModalAsync();
        };
    }
}