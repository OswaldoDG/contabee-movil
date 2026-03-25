using System.Text.RegularExpressions;
using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Pages.Perfil;

public partial class ManualRegistroPage : ContentPage
{
    // Física: 4 letras (A-Z + Ñ, sin acentos) + fecha + homoclave = 13 chars
    private static readonly Regex RfcFisicaRegex = new(
        @"^[A-ZÑ]{4}\d{2}(0[1-9]|1[0-2])(0[1-9]|[12][0-9]|3[01])[A-Z0-9]{3}$",
        RegexOptions.Compiled);

    // Moral: 3 letras (A-Z + Ñ + &) + fecha + homoclave = 12 chars
    private static readonly Regex RfcMoralRegex = new(
        @"^[A-ZÑ&]{3}\d{2}(0[1-9]|1[0-2])(0[1-9]|[12][0-9]|3[01])[A-Z0-9]{3}$",
        RegexOptions.Compiled);

    private static readonly Regex CpRegex = new(@"^\d{5}$", RegexOptions.Compiled);

    private readonly IServicioCrm _servicioCrm;

    private bool EsFisica => PickerPersona.SelectedIndex == 0;

    public ManualRegistroPage(IServicioCrm servicioCrm)
    {
        InitializeComponent();
        _servicioCrm = servicioCrm;

        PickerPersona.Items.Add("Física");
        PickerPersona.Items.Add("Moral");
        PickerPersona.SelectedIndex = 0;

        PopulateRegimen();
    }

    private void EntryName_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateRegistrarButton();
    }

    private void EntryRfc_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = e?.NewTextValue ?? string.Empty;

        // Auto-mayúsculas
        if (sender is Entry entry && text != text.ToUpperInvariant())
        {
            entry.Text = text.ToUpperInvariant();
            return; // el evento se re-dispara con el texto corregido
        }

        RefreshRfcCounter(text);
        UpdateRegistrarButton();
    }

    private void EntryCp_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = e?.NewTextValue ?? string.Empty;
        var len = text.Length;
        LabelCpCounter.Text = $"{len}/5";

        bool valid = CpRegex.IsMatch(text);
        string state = valid ? "Valid" : len > 0 ? "Invalid" : "Empty";
        VisualStateManager.GoToState(LabelCpCounter, state);

        UpdateRegistrarButton();
    }

    private void PickerPersona_SelectedIndexChanged(object? sender, EventArgs e)
    {
        int nuevoMax = EsFisica ? 13 : 12;
        EntryRfc.MaxLength = nuevoMax;

        // Limpiar RFC al cambiar tipo: el formato cambia completamente (12 vs 13 chars)
        EntryRfc.Text = string.Empty;
        RefreshRfcCounter(string.Empty);

        PopulateRegimen();
        UpdateRegistrarButton();
    }

    private void PickerRegimen_SelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdateRegistrarButton();
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
        PersonaType sel = EsFisica ? PersonaType.Fisica : PersonaType.Moral;
        var list = RegimenFiscalProvider.GetRegimenFiscal(sel);
        foreach (var r in list)
            PickerRegimen.Items.Add($"{r.Codigo} - {r.Descripcion}");
    }

    private void RefreshRfcCounter(string text)
    {
        int max = EsFisica ? 13 : 12;
        LabelCounter.Text = $"{text.Length}/{max}";

        bool valid = IsRfcValid(text);
        string state = valid ? "Valid" : text.Length > 0 ? "Invalid" : "Empty";
        VisualStateManager.GoToState(LabelCounter, state);
    }

    private bool IsRfcValid(string rfc)
    {
        var regex = EsFisica ? RfcFisicaRegex : RfcMoralRegex;
        return regex.IsMatch(rfc);
    }

    private void UpdateRegistrarButton()
    {
        bool rfcValido = IsRfcValid(EntryRfc.Text?.Trim().ToUpperInvariant() ?? string.Empty);
        bool cpValido = CpRegex.IsMatch(EntryCp.Text?.Trim() ?? string.Empty);
        bool nombreValido = !string.IsNullOrWhiteSpace(EntryName.Text);
        bool regimenValido = PickerRegimen.SelectedIndex >= 0;

        BtnRegistrar.IsEnabled = rfcValido && cpValido && nombreValido && regimenValido;
    }

    private async Task Registrar()
    {
        SetLoading(true);
        try
        {
            var selPersona = EsFisica ? PersonaType.Fisica : PersonaType.Moral;
            var regimenSel = RegimenFiscalProvider.GetRegimenFiscal(selPersona)[PickerRegimen.SelectedIndex].Codigo;

            var modelo = new CuentaFiscalMinima
            {
                Nombre = EntryName.Text?.Trim() ?? string.Empty,
                Rfc = EntryRfc.Text?.Trim().ToUpperInvariant() ?? string.Empty,
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
            await Navigation.PopModalAsync();

            if (Shell.Current != null)
                await Shell.Current.GoToAsync("..");
            else
            {
                var shell = MauiProgram.Services.GetRequiredService<AppShell>();
                Application.Current!.Windows[0].Page = shell;
            }
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
