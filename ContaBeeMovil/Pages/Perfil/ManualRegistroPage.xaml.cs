using System.Text.RegularExpressions;
using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Perfil;

public partial class ManualRegistroPage : ContentPage
{
    private static readonly List<string> EntidadesFederativas =
    [
        "AGUASCALIENTES",
        "BAJA CALIFORNIA",
        "BAJA CALIFORNIA SUR",
        "CAMPECHE",
        "COAHUILA",
        "COLIMA",
        "CHIAPAS",
        "CHIHUAHUA",
        "CIUDAD DE MEXICO",
        "DURANGO",
        "GUANAJUATO",
        "GUERRERO",
        "HIDALGO",
        "JALISCO",
        "MEXICO",
        "MICHOACAN",
        "MORELOS",
        "NAYARIT",
        "NUEVO LEON",
        "OAXACA",
        "PUEBLA",
        "QUERETARO",
        "QUINTANA ROO",
        "SAN LUIS POTOSI",
        "SINALOA",
        "SONORA",
        "TABASCO",
        "TAMAULIPAS",
        "TLAXCALA",
        "VERACRUZ",
        "YUCATAN",
        "ZACATECAS"
    ];

    private static readonly Regex RfcFisicaRegex = new(
        @"^[A-ZÑ]{4}\d{2}(0[1-9]|1[0-2])(0[1-9]|[12][0-9]|3[01])[A-Z0-9]{3}$",
        RegexOptions.Compiled);

    private static readonly Regex RfcMoralRegex = new(
        @"^[A-ZÑ&]{3}\d{2}(0[1-9]|1[0-2])(0[1-9]|[12][0-9]|3[01])[A-Z0-9]{3}$",
        RegexOptions.Compiled);

    private static readonly Regex CpRegex = new(@"^\d{5}$", RegexOptions.Compiled);

    private readonly IServicioCrm _servicioCrm;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioLogs _logs;
    private readonly IServicioToast _servicioToast;
    private bool _isLoading;

    private bool EsFisica => SelectorPersona.IndiceSeleccionado == 0;

    public ManualRegistroPage(IServicioCrm servicioCrm, IServicioAlerta servicioAlerta, IServicioLogs logs, IServicioToast servicioToast)
    {
        InitializeComponent();
        _servicioCrm = servicioCrm;
        _servicioAlerta = servicioAlerta;
        _logs = logs;
        _servicioToast = servicioToast;

        SelectorPersona.Elementos = new List<string> { "Física", "Moral" };
        SelectorPersona.IndiceSeleccionado = 0;
        SelectorPersona.IndiceCambiado += OnPersonaCambiada;

        SelectorRegimen.IndiceCambiado += (_, _) => UpdateRegistrarButton();

        SelectorEntidadFederativa.Elementos = EntidadesFederativas;
        SelectorEntidadFederativa.IndiceSeleccionado = -1;

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
        await Navigation.PopAsync();
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
        if (_isLoading)
        {
            BtnRegistrar.IsEnabled = false;
            return;
        }

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
                EntidadFederativa = SelectorEntidadFederativa.IndiceSeleccionado >= 0
                    ? EntidadesFederativas[SelectorEntidadFederativa.IndiceSeleccionado]
                    : string.Empty,
                Municipio = EntryMunicipio.Text?.Trim() ?? string.Empty,
                Colonia = EntryColonia.Text?.Trim() ?? string.Empty,
                NombreVialidad = EntryNombreVialidad.Text?.Trim() ?? string.Empty,
                NumExterior = EntryNumExterior.Text?.Trim() ?? string.Empty,
                NumInterior = EntryNumInterior.Text?.Trim() ?? string.Empty,
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

            LimpiarFormulario();

            if (Navigation.NavigationStack.Count > 1)
            {
                var stack = Navigation.NavigationStack;
                if (stack.Count >= 2 && stack[^2] is RegistrarRFCsPage registrarPage)
                    Navigation.RemovePage(registrarPage);

                await Navigation.PopAsync();
                await _servicioToast.MostrarAsync("Cuenta fiscal registrada correctamente.");
            }
            else if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync(nameof(RFCsPage));
                await _servicioToast.MostrarAsync("Cuenta fiscal registrada correctamente.");
            }
        }
        catch (HttpRequestException ex)
        {
            _logs.Log($"[ManualRegistroPage] HttpRequestException: {ex.Message}");
            await _servicioAlerta.MostrarAsync("Error de conexión", "No se pudo conectar al servidor. Verifica tu conexión.", verBotonCancelar: false, confirmarText: "OK");
        }
        catch (TaskCanceledException ex)
        {
            _logs.Log($"[ManualRegistroPage] TaskCanceledException: {ex.Message}");
            await _servicioAlerta.MostrarAsync("Error de tiempo", "La solicitud tardó demasiado. Intenta de nuevo.", verBotonCancelar: false, confirmarText: "OK");
        }
        catch (Exception ex)
        {
            _logs.Log($"[ManualRegistroPage] {ex.GetType().Name}: {ex.Message}");
            await _servicioAlerta.MostrarAsync("Error inesperado", "Ocurrió un error inesperado. Intenta de nuevo.", verBotonCancelar: false, confirmarText: "OK");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        _isLoading = isLoading;
        LoadingOverlay.IsVisible = isLoading;
        BtnCancel.IsEnabled = !isLoading;
        if (isLoading)
        {
            BtnRegistrar.IsEnabled = false;
            return;
        }

        UpdateRegistrarButton();
    }

    private void LimpiarFormulario()
    {
        EntryName.Text = string.Empty;
        EntryRfc.Text = string.Empty;
        EntryCp.Text = string.Empty;
        EntryMunicipio.Text = string.Empty;
        EntryColonia.Text = string.Empty;
        EntryNombreVialidad.Text = string.Empty;
        EntryNumExterior.Text = string.Empty;
        EntryNumInterior.Text = string.Empty;
        SelectorPersona.IndiceSeleccionado = 0;
        SelectorRegimen.IndiceSeleccionado = -1;
        SelectorEntidadFederativa.IndiceSeleccionado = -1;
        RefreshRfcCounter(string.Empty);
        VisualStateManager.GoToState(LabelCpCounter, "Empty");
        LabelCpCounter.Text = "0/5";
        ChkCompartido.IsChecked = true;
    }
}
