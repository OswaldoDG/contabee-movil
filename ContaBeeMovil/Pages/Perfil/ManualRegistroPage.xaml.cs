using System.Text.RegularExpressions;
using Contabee.Api;
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
    private const string SinDireccionTextoBase = "No deseo proporcionar mi dirección";
    private const string SinDireccionTextoCompleto = "No deseo proporcionar mi dirección: Y estoy de acuerdo que Contabee no deberá obtener el CFDI si la información es obligatoria en el portal de facturación.";

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
    private bool _esEdicion;
    private AsociacionCuentaFiscalCompleta? _cuentaEnEdicion;
    private string _ultimoRfcValido = string.Empty;

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
        SelectorEntidadFederativa.IndiceCambiado += (_, _) => UpdateRegistrarButton();

        PopulateRegimen();
        ApplySinDireccionState(false);
    }

    public void ConfigurarAlta()
    {
        _esEdicion = false;
        _cuentaEnEdicion = null;
        Title = "Nueva cuenta fiscal";
        BtnRegistrar.Texto = "Registrar";
        EntryRfc.IsReadOnly = false;
        EntryRfc.Opacity = 1;
        BloquearTipoYRegimen(false);
        LimpiarFormulario();
    }

    public void ConfigurarEdicion(AsociacionCuentaFiscalCompleta cuenta)
    {
        _esEdicion = true;
        _cuentaEnEdicion = cuenta;
        Title = "Editar cuenta fiscal";
        BtnRegistrar.Texto = "Actualizar";
        EntryRfc.IsReadOnly = true;
        EntryRfc.Opacity = 1;
        BloquearTipoYRegimen(true);

        var cuentaFiscal = cuenta.CuentaFiscal;
        var direccion = cuentaFiscal?.Direcciones?.FirstOrDefault() ?? cuenta.DireccionesFiscales?.FirstOrDefault();

        bool esFisica = cuentaFiscal != null
            ? cuentaFiscal.Tipo == TipoPersonaFiscal.Fisica
            : (cuenta.Rfc?.Trim().Length ?? 0) >= 13;
        SelectorPersona.IndiceSeleccionado = esFisica ? 0 : 1;
        PopulateRegimen();

        EntryName.Text = cuentaFiscal?.Nombre ?? string.Empty;
        EntryRfc.Text = (cuenta.Rfc ?? string.Empty).Trim().ToUpperInvariant();
        EntryCp.Text = direccion?.CodigoPostal ?? string.Empty;
        EntryMunicipio.Text = direccion?.Municipio ?? string.Empty;
        EntryColonia.Text = direccion?.Colonia ?? string.Empty;
        EntryNombreVialidad.Text = direccion?.NombreVialidad ?? string.Empty;
        EntryNumExterior.Text = direccion?.NumExterior ?? string.Empty;
        EntryNumInterior.Text = direccion?.NumInterior ?? string.Empty;

        var regimenes = RegimenFiscalProvider.GetRegimenFiscal(esFisica ? PersonaType.Fisica : PersonaType.Moral);
        var claveRegimen = cuentaFiscal?.ClaveRegimenFiscal ?? cuenta.ClaveRegimenFiscal;
        SelectorRegimen.IndiceSeleccionado = regimenes.FindIndex(r => r.Codigo == claveRegimen);

        var entidad = direccion?.EntidadFederativa?.Trim().ToUpperInvariant() ?? string.Empty;
        SelectorEntidadFederativa.IndiceSeleccionado = EntidadesFederativas.FindIndex(e => e == entidad);

        ChkSinDireccion.IsChecked = cuentaFiscal?.SinDireccion ?? false;
        ChkCompartido.IsChecked = cuentaFiscal?.Compartida ?? ChkCompartido.IsChecked;

        RefreshRfcCounter(EntryRfc.Text ?? string.Empty);
        EntryCp_TextChanged(EntryCp, new TextChangedEventArgs(string.Empty, EntryCp.Text ?? string.Empty));
        UpdateRegistrarButton();
    }

    private void BloquearTipoYRegimen(bool bloqueado)
    {
        SelectorPersona.IsEnabled = !bloqueado;
        SelectorRegimen.IsEnabled = !bloqueado;
        SelectorPersona.Opacity = bloqueado ? 0.5 : 1;
        SelectorRegimen.Opacity = bloqueado ? 0.5 : 1;
        LblEdicionBloqueado.IsVisible = bloqueado;
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

        if (!string.IsNullOrEmpty(text))
            _ultimoRfcValido = text;

        RefreshRfcCounter(text);
        UpdateRegistrarButton();
    }

    private void EntryRfc_Unfocused(object? sender, FocusEventArgs e)
    {
        if (string.IsNullOrEmpty(EntryRfc.Text) && !string.IsNullOrEmpty(_ultimoRfcValido))
            EntryRfc.Text = _ultimoRfcValido;
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

        var rfcActual = EntryRfc.Text ?? string.Empty;
        if (rfcActual.Length > nuevoMax)
            EntryRfc.Text = rfcActual[..nuevoMax];
        else
            RefreshRfcCounter(rfcActual);

        PopulateRegimen();
        UpdateRegistrarButton();
    }

    private void ChkSinDireccion_CheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        ApplySinDireccionState(e.Value);
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
            // El spinner del botón (EstaCargando) ya refleja el estado de carga;
            // no tocar IsEnabled para no atenuarlo bajo el spinner.
            return;
        }

        if (_esEdicion && _cuentaEnEdicion?.TipoCuenta != TipoCuenta.Primaria)
        {
            BtnRegistrar.IsEnabled = false;
            return;
        }

        string rfc = EntryRfc.Text?.Trim().ToUpperInvariant() ?? string.Empty;
        string cp = EntryCp.Text?.Trim() ?? string.Empty;
        bool rfcValido = IsRfcValid(rfc);
        bool cpValido = CpRegex.IsMatch(cp);
        bool nombreValido = !string.IsNullOrWhiteSpace(EntryName.Text);
        bool regimenValido = SelectorRegimen.IndiceSeleccionado >= 0;

        bool direccionValida = ChkSinDireccion.IsChecked || SelectorEntidadFederativa.IndiceSeleccionado >= 0;

        ActualizarErroresEnLinea(rfc, rfcValido, cp, cpValido, nombreValido, regimenValido);

        if (LblDireccionError.IsVisible && direccionValida)
            LblDireccionError.IsVisible = false;

        BtnRegistrar.IsEnabled = true;
    }

    private void ActualizarErroresEnLinea(string rfc, bool rfcValido, string cp, bool cpValido, bool nombreValido, bool regimenValido, bool enSubmit = false)
    {
        if (rfcValido || (!enSubmit && rfc.Length == 0))
        {
            LblRfcError.IsVisible = false;
        }
        else
        {
            int rfcMax = EsFisica ? 13 : 12;
            int faltantes = rfcMax - rfc.Length;
            LblRfcError.Text = faltantes > 0
                ? $"El RFC debe tener {rfcMax} caracteres."
                : "El formato del RFC no es válido.";
            LblRfcError.IsVisible = true;
        }

        if (cpValido || (!enSubmit && cp.Length == 0))
            LblCpError.IsVisible = false;
        else
        {
            LblCpError.Text = "El código postal debe tener 5 dígitos.";
            LblCpError.IsVisible = true;
        }

        if (nombreValido || !enSubmit)
            LblNombreError.IsVisible = false;
        else
        {
            LblNombreError.Text = "El nombre o razón social es requerido.";
            LblNombreError.IsVisible = true;
        }

        if (regimenValido || !enSubmit)
            LblRegimenError.IsVisible = false;
        else
        {
            LblRegimenError.Text = "Selecciona un régimen fiscal.";
            LblRegimenError.IsVisible = true;
        }
    }

    private async Task Registrar()
    {
        if (_esEdicion && _cuentaEnEdicion?.TipoCuenta != TipoCuenta.Primaria)
        {
            await _servicioAlerta.MostrarAsync(
                "No permitido",
                "Solo puedes actualizar cuentas fiscales primarias.",
                verBotonCancelar: false,
                confirmarText: "OK");
            return;
        }

        string rfc = EntryRfc.Text?.Trim().ToUpperInvariant() ?? string.Empty;
        string cp = EntryCp.Text?.Trim() ?? string.Empty;
        bool rfcValido = IsRfcValid(rfc);
        bool cpValido = CpRegex.IsMatch(cp);
        bool nombreValido = !string.IsNullOrWhiteSpace(EntryName.Text);
        bool regimenValido = SelectorRegimen.IndiceSeleccionado >= 0;
        bool direccionValida = ChkSinDireccion.IsChecked || SelectorEntidadFederativa.IndiceSeleccionado >= 0;

        if (!rfcValido || !cpValido || !nombreValido || !regimenValido || !direccionValida)
        {
            ActualizarErroresEnLinea(rfc, rfcValido, cp, cpValido, nombreValido, regimenValido, enSubmit: true);
            LblDireccionError.IsVisible = !direccionValida;
            if (!direccionValida)
                LblDireccionError.Text = "Ingresa tu domicilio o confirma que no deseas proporcionarlo.";
            return;
        }

        SetLoading(true);
        try
        {
            var selPersona = EsFisica ? PersonaType.Fisica : PersonaType.Moral;
            var regimenSel = RegimenFiscalProvider.GetRegimenFiscal(selPersona)[SelectorRegimen.IndiceSeleccionado].Codigo;

            var modelo = new CuentaFiscalMinima
            {
                Tipo = EsFisica ? TipoPersonaFiscal.Fisica : TipoPersonaFiscal.Moral,
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
                Compartido = ChkCompartido.IsChecked,
                SinDireccion = ChkSinDireccion.IsChecked
            };

            if (ChkSinDireccion.IsChecked)
            {
                modelo.EntidadFederativa = "-";
                modelo.Municipio = "-";
                modelo.Colonia = "-";
                modelo.NombreVialidad = "-";
                modelo.NumExterior = "-";
                modelo.NumInterior = "-";
            }

            var resp = _esEdicion
                ? await _servicioCrm.ActualizaRFCMinima(modelo)
                : await _servicioCrm.RegistrarCuentaFiscalMinima(modelo);
            if (!resp.Ok)
            {
                SetLoading(false);
                var mensajeError = _esEdicion ? "Error al actualizar." : "Error al registrar.";
                await _servicioAlerta.MostrarAsync("Error", resp.Error?.Mensaje ?? mensajeError, verBotonCancelar: false, confirmarText: "OK");
                return;
            }

            var cuentas = await _servicioCrm.GetAsociacionesFiscales();
            if (cuentas.Ok && cuentas.Payload != null)
            {
                AppState.Instance.CuentasFiscales = cuentas.Payload;
                AppState.Instance.CuentaFiscalActual ??= cuentas.Payload.FirstOrDefault();
            }

            if (Navigation.NavigationStack.Count > 1)
            {
                var stack = Navigation.NavigationStack;
                if (stack.Count >= 2 && stack[^2] is RegistrarRFCsPage registrarPage)
                    Navigation.RemovePage(registrarPage);

                await Navigation.PopAsync();
                await _servicioToast.MostrarAsync(_esEdicion
                    ? "Cuenta fiscal actualizada correctamente."
                    : "Cuenta fiscal registrada correctamente.");
            }
            else if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync(nameof(RFCsPage));
                await _servicioToast.MostrarAsync(_esEdicion
                    ? "Cuenta fiscal actualizada correctamente."
                    : "Cuenta fiscal registrada correctamente.");
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
        BtnRegistrar.EstaCargando = isLoading;
        BtnCancel.IsEnabled = !isLoading;
        if (isLoading)
            return;

        UpdateRegistrarButton();
    }

    private void LimpiarFormulario()
    {
        _ultimoRfcValido = string.Empty;
        LblNombreError.IsVisible = false;
        LblRegimenError.IsVisible = false;
        LblDireccionError.IsVisible = false;
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
        ChkSinDireccion.IsChecked = false;
        RefreshRfcCounter(string.Empty);
        VisualStateManager.GoToState(LabelCpCounter, "Empty");
        LabelCpCounter.Text = "0/5";
        ChkCompartido.IsChecked = true;
        UpdateRegistrarButton();
    }

    private void ApplySinDireccionState(bool sinDireccion)
    {
        DireccionContainer.IsVisible = !sinDireccion;
        LblSinDireccionTexto.Text = sinDireccion ? SinDireccionTextoCompleto : SinDireccionTextoBase;
    }
}
