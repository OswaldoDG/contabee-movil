using System.Text.RegularExpressions;
using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Pages.Perfil;

public partial class ManualRegistroPage : ContentPage
{
    private static readonly Regex RfcFisicaRegex = new(
        @"^[A-ZÑ]{4}\d{2}(0[1-9]|1[0-2])(0[1-9]|[12][0-9]|3[01])[A-Z0-9]{3}$",
        RegexOptions.Compiled);

    private static readonly Regex RfcMoralRegex = new(
        @"^[A-ZÑ&]{3}\d{2}(0[1-9]|1[0-2])(0[1-9]|[12][0-9]|3[01])[A-Z0-9]{3}$",
        RegexOptions.Compiled);

    private static readonly Regex CpRegex = new(@"^\d{5}$", RegexOptions.Compiled);

    private readonly IServicioCrm _servicioCrm;
    private readonly IServicioAlerta _servicioAlerta;

    private bool EsFisica => SelectorPersona.IndiceSeleccionado == 0;

    public ManualRegistroPage(IServicioCrm servicioCrm, IServicioAlerta servicioAlerta)
    {
        InitializeComponent();
        _servicioCrm = servicioCrm;
        _servicioAlerta = servicioAlerta;

        SelectorPersona.Elementos = new List<string> { "Física", "Moral" };
        SelectorPersona.IndiceSeleccionado = 0;
        SelectorPersona.IndiceCambiado += OnPersonaCambiada;

        SelectorRegimen.IndiceCambiado += (_, _) => UpdateRegistrarButton();

        PopulateRegimen();
    }

    private void EntryName_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateRegistrarButton();
    }

    private void EntryRfc_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = e?.NewTextValue ?? string.Empty;

        if (sender is Entry entry && text != text.ToUpperInvariant())
        {
            entry.Text = text.ToUpperInvariant();
            return;
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

    private void OnPersonaCambiada(object? sender, int indice)
    {
        int nuevoMax = EsFisica ? 13 : 12;
        EntryRfc.MaxLength = nuevoMax;

        EntryRfc.Text = string.Empty;
        RefreshRfcCounter(string.Empty);

        PopulateRegimen();
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
        PersonaType sel = EsFisica ? PersonaType.Fisica : PersonaType.Moral;
        var list = RegimenFiscalProvider.GetRegimenFiscal(sel);
        SelectorRegimen.Elementos = list.Select(r => $"{r.Codigo} - {r.Descripcion}").ToList();
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
        bool regimenValido = SelectorRegimen.IndiceSeleccionado >= 0;

        BtnRegistrar.IsEnabled = rfcValido && cpValido && nombreValido && regimenValido;
    }

    private async Task Registrar()
    {
        SetLoading(true);
        try
        {
            var selPersona = EsFisica ? PersonaType.Fisica : PersonaType.Moral;
            var regimenSel = RegimenFiscalProvider.GetRegimenFiscal(selPersona)[SelectorRegimen.IndiceSeleccionado].Codigo;

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
                await _servicioAlerta.MostrarAsync("Error", resp.Error?.Mensaje ?? "Error al registrar.", verBotonCancelar: false, confirmarText: "OK");
                return;
            }

            var cuentas = await _servicioCrm.GetAsociacionesFiscales();
            if (cuentas.Ok && cuentas.Payload != null)
            {
                AppState.Instance.CuentasFiscales = cuentas.Payload;
                AppState.Instance.CuentaFiscalActual ??= cuentas.Payload.FirstOrDefault();
            }

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
            await _servicioAlerta.MostrarAsync("Error de conexión", $"No se pudo conectar al servidor: {ex.Message}", verBotonCancelar: false, confirmarText: "OK");
        }
        catch (TaskCanceledException ex)
        {
            SetLoading(false);
            await _servicioAlerta.MostrarAsync("Error de tiempo", $"La solicitud tardó demasiado: {ex.Message}", verBotonCancelar: false, confirmarText: "OK");
        }
        catch (Exception ex)
        {
            SetLoading(false);
            await _servicioAlerta.MostrarAsync("Error inesperado", $"Ocurrió un error: {ex.Message}", verBotonCancelar: false, confirmarText: "OK");
        }
    }

    private void SetLoading(bool isLoading)
    {
        LoadingOverlay.IsVisible = isLoading;
        BtnRegistrar.IsEnabled = !isLoading;
        BtnCancel.IsEnabled = !isLoading;
    }
}
