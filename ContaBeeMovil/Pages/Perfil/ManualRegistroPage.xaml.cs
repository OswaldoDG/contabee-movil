using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Pages.Perfil;

public partial class ManualRegistroPage : ContentPage
{
    private readonly IServicioCrm _servicioCrm;

    public ManualRegistroPage(IServicioCrm servicioCrm)
    {
        InitializeComponent();
        _servicioCrm = servicioCrm;

        PickerPersona.Items.Add("Física");
        PickerPersona.Items.Add("Moral");
        PickerPersona.SelectedIndex = 0;

        PopulateRegimen();
    }

    private void EntryName_TextChanged(object? sender, TextChangedEventArgs e) { }

    private void EntryRfc_TextChanged(object? sender, TextChangedEventArgs e)
    {
        LabelCounter.Text = $"{(e?.NewTextValue?.Length ?? 0)}/13";
    }

    private void PickerPersona_SelectedIndexChanged(object? sender, EventArgs e)
    {
        PopulateRegimen();
    }

    private async void BtnCancel_Clicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private async void BtnRegistrar_Clicked(object? sender, EventArgs e)
    {
        await Registrar();
    }

    private void PopulateRegimen()
    {
        PickerRegimen.Items.Clear();
        PersonaType sel = PickerPersona.SelectedIndex == 0 ? PersonaType.Fisica : PersonaType.Moral;
        var list = RegimenFiscalProvider.GetRegimenFiscal(sel);
        foreach (var r in list)
            PickerRegimen.Items.Add($"{r.Codigo} - {r.Descripcion}");
    }

    private async Task Registrar()
    {
        var rfc = EntryRfc.Text?.Trim() ?? string.Empty;
        if (rfc.Length < 12 || rfc.Length > 13)
        {
            await DisplayAlert("RFC inválido", "El RFC debe tener 12 o 13 caracteres.", "OK");
            return;
        }

        SetLoading(true);
        try
        {
            var regimenSel = string.Empty;
            if (PickerRegimen.SelectedIndex >= 0)
            {
                var selPersona = PickerPersona.SelectedIndex == 0 ? PersonaType.Fisica : PersonaType.Moral;
                regimenSel = RegimenFiscalProvider.GetRegimenFiscal(selPersona)[PickerRegimen.SelectedIndex].Codigo;
            }

            var modelo = new CuentaFiscalMinima
            {
                Nombre = EntryName.Text?.Trim() ?? string.Empty,
                Rfc = rfc,
                CodigoPostal = EntryCp.Text?.Trim() ?? string.Empty,
                ClaveRegimenFiscal = regimenSel,
                Compartido = ChkCompartido.IsChecked
            };

            var resp = await _servicioCrm.RegistrarCuentaFiscalMinima(modelo);
            if (!resp.Ok)
            {
                SetLoading(false);
                await DisplayAlert("Error", resp.Error?.Mensaje ?? "Error al registrar.", "OK");
                return;
            }

            var cuentas = await _servicioCrm.GetAsociacionesFiscales();
            if (cuentas.Ok && cuentas.Payload != null)
                AppState.Instance.CuentasFiscales = cuentas.Payload;

            SetLoading(false);
            await DisplayAlert("Éxito", "Cuenta fiscal registrada.", "OK");
            await Navigation.PopModalAsync();
        }
        catch (HttpRequestException ex)
        {
            SetLoading(false);
            await DisplayAlert("Error de conexión", $"No se pudo conectar al servidor: {ex.Message}", "OK");
        }
        catch (TaskCanceledException ex)
        {
            SetLoading(false);
            await DisplayAlert("Error de tiempo", $"La solicitud tardó demasiado: {ex.Message}", "OK");
        }
        catch (Exception ex)
        {
            SetLoading(false);
            await DisplayAlert("Error inesperado", $"Ocurrió un error: {ex.Message}", "OK");
        }
    }

    private void SetLoading(bool isLoading)
    {
        LoadingOverlay.IsVisible = isLoading;
        BtnRegistrar.IsEnabled = !isLoading;
        BtnCancel.IsEnabled = !isLoading;
    }
}
