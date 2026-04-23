using System.Collections.ObjectModel;
using System.Windows.Input;
using Contabee.Api.abstractions;
using Contabee.Api.Transcript;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Models;
using ContaBeeMovil.Pages.Perfil;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Camara;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;
using MauiIcons.Core;
using MauiIcons.Material;

namespace ContaBeeMovil.Pages.Captura;

public partial class PaginaCaptura : ContentPage, IQueryAttributable
{
    private readonly IServicioCamara _servicioCamara;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IToastService _toastService;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioTranscript _servicioTranscript;
    private readonly IServicioLogs _logs;

    // ── Preferencias recordadas ──────────────────────────────────────────────

    private const string PrefFormaPago = "captura_forma_pago";
    private const string PrefTarjeta   = "captura_tarjeta_id";
    private const string PrefUsoCfdi   = "captura_uso_cfdi";
    private const string PrefDesgIeps  = "captura_desg_ieps";
    private const string PrefNotas     = "captura_notas";

    // ── Constructor ──────────────────────────────────────────────────────────

    public PaginaCaptura(IServicioCamara servicioCamara, IServicioAlerta servicioAlerta, IToastService toastService, IServicioSesion servicioSesion, IServicioTranscript servicioTranscript, IServicioLogs logs)
    {
        _servicioCamara    = servicioCamara;
        _servicioAlerta    = servicioAlerta;
        _toastService      = toastService;
        _servicioSesion    = servicioSesion;
        _servicioTranscript = servicioTranscript;
        _logs              = logs;

        FormasPago = FormaPagoProvider.GetFormasPago();
        _capturas  = new ObservableCollection<CapturaLote>(AppState.Instance.CapturasLote ?? []);
        _capturas.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TieneCapturas));
            OnPropertyChanged(nameof(ColumnSpanCamara));
            OnPropertyChanged(nameof(PuedeEnviar));
        };
        ActualizarUsoCfdi();

        TomarFotoCommand        = new Command(async () => await TomarFotoAsync());
        EliminarCapturaCommand  = new Command<CapturaLote>(async c => await EliminarCapturaAsync(c));
        VerImagenCommand        = new Command<CapturaLote>(async c => await VerImagenAsync(c));
        EnviarCommand           = new Command(async () => await EnviarAsync());
        CancelarCommand         = new Command(async () => await CancelarAsync());
        IrAgregarTarjetaCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(TarjetasPage)));

        InitializeComponent();

        BtnCamaraSin.Icon(MaterialIcons.PhotoCamera).IconSize(28);
        BtnCamaraCon.Icon(MaterialIcons.PhotoCamera).IconSize(28);

        CircularProgress.Drawable = _progressDrawable;

        SelectorFormaPago.Elementos = FormasPago.Select(f => f.Descripcion).ToList();
        SelectorFormaPago.IndiceCambiado += OnFormaPagoCambiada;
        SelectorTarjeta.IndiceCambiado   += OnTarjetaCambiada;
        SelectorUsoCfdi.Elementos = _usoCfdiOpciones.Select(u => u.Descripcion).ToList();
        SelectorUsoCfdi.IndiceCambiado   += OnUsoCfdiCambiado;

        RestaurarPreferencias();

        BindingContext = this;

        AppState.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.Licenciamiento))
                OnPropertyChanged(nameof(CreditosCaptura));
        };
    }

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("tipo", out var t) && t is TipoProcesoCaptura tipo)
            TipoCaptura = tipo;

        _capturas.Clear();
        _pendienteVerificarFotos = true;

        ActualizarUsoCfdi();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = CargarTarjetasYRefrescarAsync();

        if (_pendienteVerificarFotos)
        {
            _pendienteVerificarFotos = false;
            _ = InicializarCapturasAsync();
        }
    }

    private async Task InicializarCapturasAsync()
    {
        await VerificarFotosGuardadasAsync();

        var sharedFileName = SharedImageHandler.TakePendingSharedImage();
        if (string.IsNullOrEmpty(sharedFileName)) return;

        if (_capturas.Count > 0)
        {
            var mantener = await _servicioAlerta.MostrarAsync(
                "Foto compartida",
                $"Tienes {_capturas.Count} captura(s) guardada(s). ¿Deseas mantenerlas junto con la foto compartida?",
                confirmarText: "Mantener",
                cancelarText: "Eliminar");

            if (!mantener)
            {
                foreach (var c in _capturas.ToList())
                {
                    try { File.Delete(c.Path); } catch { /* ignorar */ }
                }
                _capturas.Clear();

                var restantes = (AppState.Instance.CapturasLote ?? [])
                    .Where(c => c.TipoCaptura != TipoCaptura)
                    .ToList();
                AppState.Instance.CapturasLote = restantes.Count > 0 ? restantes : null;
            }
        }

        var captura = new CapturaLote { TipoCaptura = TipoCaptura, FileName = sharedFileName };
        _capturas.Add(captura);
        AppState.Instance.CapturasLote = [.. _capturas];
    }

    private bool _pendienteVerificarFotos;

    private async Task VerificarFotosGuardadasAsync()
    {
        var loteCompleto = AppState.Instance.CapturasLote ?? [];
        _logs.Log($"[PaginaCaptura] VerificarFotos — AppDataDirectory={FileSystem.AppDataDirectory}");
        _logs.Log($"[PaginaCaptura] VerificarFotos — TipoCaptura={TipoCaptura}, total en AppState={loteCompleto.Count}");

        var capturasGuardadas = loteCompleto
            .Where(c => c.TipoCaptura == TipoCaptura)
            .ToList();

        _logs.Log($"[PaginaCaptura] VerificarFotos — del tipo actual={capturasGuardadas.Count}");

        foreach (var c in capturasGuardadas)
        {
            var existe = File.Exists(c.Path);
            _logs.Log($"[PaginaCaptura] VerificarFotos — path={c.Path} | existe={existe}");
        }

        capturasGuardadas = capturasGuardadas.Where(c => File.Exists(c.Path)).ToList();
        _logs.Log($"[PaginaCaptura] VerificarFotos — con archivo en disco={capturasGuardadas.Count}");

        if (capturasGuardadas.Count == 0) return;

        bool conservar = await _servicioAlerta.MostrarAsync(
            "Imágenes guardadas",
            $"Tienes {capturasGuardadas.Count} imagen(es) de una captura anterior. ¿Deseas conservarlas?",
            confirmarText: "Conservar",
            cancelarText: "Eliminar");

        _logs.Log($"[PaginaCaptura] VerificarFotos — usuario eligió conservar={conservar}");

        if (conservar)
        {
            foreach (var c in capturasGuardadas)
                _capturas.Add(c);
        }
        else
        {
            foreach (var c in capturasGuardadas)
            {
                try
                {
                    File.Delete(c.Path);
                    _logs.Log($"[PaginaCaptura] VerificarFotos — archivo eliminado: {c.Path}");
                }
                catch (Exception ex)
                {
                    _logs.Log($"[PaginaCaptura] VerificarFotos — error al eliminar {c.Path}: {ex.Message}");
                }
            }

            var restantes = (AppState.Instance.CapturasLote ?? [])
                .Where(c => c.TipoCaptura != TipoCaptura)
                .ToList();
            AppState.Instance.CapturasLote = restantes.Count > 0 ? restantes : null;
            _logs.Log($"[PaginaCaptura] VerificarFotos — AppState actualizado, restantes={restantes.Count}");
        }
    }

    private async Task CargarTarjetasYRefrescarAsync()
    {
        await _servicioSesion.GetLicenciaAsync();
        if (AppState.Instance.Tarjetas is null)
            await _servicioSesion.GetTarjetasAsync();
        RefrescarTarjetas();
    }

    // ── Parámetro de navegación ──────────────────────────────────────────────

    private TipoProcesoCaptura _tipoCaptura;
    public TipoProcesoCaptura TipoCaptura
    {
        get => _tipoCaptura;
        set { _tipoCaptura = value; OnPropertyChanged(); }
    }

    // ── Forma de Pago ────────────────────────────────────────────────────────

    public List<FormaPago> FormasPago { get; }

    private FormaPago? _formaPagoSeleccionada;

    public bool MostrarTarjetas          => _formaPagoSeleccionada?.Codigo is "4" or "28";
    public bool MostrarSelectorTarjeta   => MostrarTarjetas && (AppState.Instance.Tarjetas?.Count ?? 0) > 0;
    public bool MostrarBotonAgregarTarjeta => MostrarTarjetas && (AppState.Instance.Tarjetas?.Count ?? 0) == 0;

    public bool PuedeEnviar =>
        TieneCapturas &&
        _formaPagoSeleccionada is not null &&
        _usoCfdiSeleccionado is not null &&
        (!MostrarTarjetas || _tarjetaSeleccionada is not null);

    // ── Tarjetas ─────────────────────────────────────────────────────────────

    private TarjetaModel? _tarjetaSeleccionada;

    // ── Uso CFDI ─────────────────────────────────────────────────────────────

    private List<UsoCfdi> _usoCfdiOpciones = [];
    private UsoCfdi? _usoCfdiSeleccionado;

    // ── Desglosar IEPS ───────────────────────────────────────────────────────

    private bool _desglosarIeps;
    public bool DesglosarIeps
    {
        get => _desglosarIeps;
        set
        {
            _desglosarIeps = value;
            OnPropertyChanged();
            Preferences.Default.Set(PrefDesgIeps, value);
        }
    }

    // ── Notas adicionales ────────────────────────────────────────────────────

    private string _notasAdicionales = string.Empty;
    public string NotasAdicionales
    {
        get => _notasAdicionales;
        set
        {
            _notasAdicionales = value;
            OnPropertyChanged();
            Preferences.Default.Set(PrefNotas, value);
        }
    }

    // ── Capturas ─────────────────────────────────────────────────────────────

    private readonly ObservableCollection<CapturaLote> _capturas;
    public ObservableCollection<CapturaLote> Capturas => _capturas;

    public bool TieneCapturas    => _capturas.Count > 0;
    public int  ColumnSpanCamara => TieneCapturas ? 1 : 2;
    public int  CreditosCaptura  =>
        (AppState.Instance.Licenciamiento?.CreditosCaptura ?? 0) -
        (AppState.Instance.Licenciamiento?.CreditosCapturaConsumo ?? 0);

    // ── Ancho dinámico de cada card en el carrusel ───────────────────────────

    private double _capturaItemWidth = 300;
    public double CapturaItemWidth
    {
        get => _capturaItemWidth;
        private set { _capturaItemWidth = value; OnPropertyChanged(); }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0)
            CapturaItemWidth = width - 24; // 12 px de margen a cada lado
    }

    // ── Progreso de envío ────────────────────────────────────────────────────

    private readonly CircularProgressDrawable _progressDrawable = new();

    private double _enviandoProgreso;
    public double EnviandoProgreso
    {
        get => _enviandoProgreso;
        private set { _enviandoProgreso = value; OnPropertyChanged(); }
    }

    private bool _estaEnviando;
    public bool EstaEnviando
    {
        get => _estaEnviando;
        private set
        {
            _estaEnviando = value;
            OnPropertyChanged();
            if (value)
            {
                _progressDrawable.Progress = 0f;
                CircularProgress?.Invalidate();
            }
        }
    }

    /// <summary>
    /// Anima suavemente el arco desde su valor actual hasta <paramref name="target"/>.
    /// Se espera con await para garantizar que el dibujo está completo antes de continuar.
    /// </summary>
    private Task AnimarProgresoAsync(float target, uint duracionMs = 350)
    {
        var tcs  = new TaskCompletionSource();
        var from = _progressDrawable.Progress;
        new Animation(v =>
        {
            _progressDrawable.Progress = (float)v;
            CircularProgress?.Invalidate();
        }, from, target, Easing.CubicOut)
        .Commit(this, "ProgresoAnim", length: duracionMs,
                finished: (_, _) => tcs.TrySetResult());
        return tcs.Task;
    }

    // ── Comandos ─────────────────────────────────────────────────────────────

    public ICommand TomarFotoCommand        { get; }
    public ICommand EliminarCapturaCommand  { get; }
    public ICommand VerImagenCommand        { get; }
    public ICommand EnviarCommand           { get; }
    public ICommand CancelarCommand         { get; }
    public ICommand IrAgregarTarjetaCommand { get; }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void RestaurarPreferencias()
    {
        // DesglosarIeps (directo al campo para no reescribir la preferencia en el setter)
        _desglosarIeps = Preferences.Default.Get(PrefDesgIeps, false);
        OnPropertyChanged(nameof(DesglosarIeps));

        // Notas adicionales
        _notasAdicionales = Preferences.Default.Get(PrefNotas, string.Empty);
        OnPropertyChanged(nameof(NotasAdicionales));

        // Forma de pago
        var codigoFP = Preferences.Default.Get(PrefFormaPago, string.Empty);
        if (!string.IsNullOrEmpty(codigoFP))
        {
            var idx = FormasPago.FindIndex(f => f.Codigo == codigoFP);
            if (idx >= 0)
            {
                _formaPagoSeleccionada = FormasPago[idx];
                SelectorFormaPago.IndiceSeleccionado = idx;
                var tarjetas = AppState.Instance.Tarjetas ?? [];
                SelectorTarjeta.Elementos = tarjetas.Select(t => t.DisplayLabel).ToList();
                OnPropertyChanged(nameof(MostrarTarjetas));
                OnPropertyChanged(nameof(MostrarSelectorTarjeta));
                OnPropertyChanged(nameof(MostrarBotonAgregarTarjeta));

                // Tarjeta (sólo si la forma de pago seleccionada la requiere)
                if (MostrarTarjetas)
                {
                    var tarjetaId = Preferences.Default.Get(PrefTarjeta, string.Empty);
                    if (!string.IsNullOrEmpty(tarjetaId))
                    {
                        var tIdx = tarjetas.FindIndex(t => t.Id == tarjetaId);
                        if (tIdx >= 0)
                        {
                            _tarjetaSeleccionada = tarjetas[tIdx];
                            SelectorTarjeta.IndiceSeleccionado = tIdx;
                        }
                    }
                }
            }
        }

        // Uso CFDI
        var codigoUso = Preferences.Default.Get(PrefUsoCfdi, string.Empty);
        if (!string.IsNullOrEmpty(codigoUso))
        {
            var idx = _usoCfdiOpciones.FindIndex(u => u.Codigo == codigoUso);
            if (idx >= 0)
            {
                _usoCfdiSeleccionado = _usoCfdiOpciones[idx];
                SelectorUsoCfdi.IndiceSeleccionado = idx;
            }
        }
    }

    private void RefrescarTarjetas()
    {
        var tarjetas = AppState.Instance.Tarjetas ?? [];
        SelectorTarjeta.Elementos = tarjetas.Select(t => t.DisplayLabel).ToList();

        // Intentar mantener la tarjeta guardada en la lista actualizada
        _tarjetaSeleccionada = null;
        SelectorTarjeta.IndiceSeleccionado = -1;
        if (MostrarTarjetas)
        {
            var tarjetaId = Preferences.Default.Get(PrefTarjeta, string.Empty);
            if (!string.IsNullOrEmpty(tarjetaId))
            {
                var idx = tarjetas.FindIndex(t => t.Id == tarjetaId);
                if (idx >= 0)
                {
                    _tarjetaSeleccionada = tarjetas[idx];
                    SelectorTarjeta.IndiceSeleccionado = idx;
                }
            }
        }

        OnPropertyChanged(nameof(MostrarSelectorTarjeta));
        OnPropertyChanged(nameof(MostrarBotonAgregarTarjeta));
        OnPropertyChanged(nameof(PuedeEnviar));
    }

    private void ActualizarUsoCfdi()
    {
        var regimen = AppState.Instance.CuentaFiscalActual?.ClaveRegimenFiscal;
        _usoCfdiOpciones    = UsoCfdiProvider.GetUsoCfdi(regimen);
        _usoCfdiSeleccionado = null;
        if (SelectorUsoCfdi is null) return;

        SelectorUsoCfdi.Elementos = _usoCfdiOpciones.Select(u => u.Descripcion).ToList();

        // Restaurar preferencia si el código guardado sigue siendo válido en el nuevo régimen
        var codigoUso = Preferences.Default.Get(PrefUsoCfdi, string.Empty);
        if (!string.IsNullOrEmpty(codigoUso))
        {
            var idx = _usoCfdiOpciones.FindIndex(u => u.Codigo == codigoUso);
            if (idx >= 0)
            {
                _usoCfdiSeleccionado = _usoCfdiOpciones[idx];
                SelectorUsoCfdi.IndiceSeleccionado = idx;
                return;
            }
        }
        SelectorUsoCfdi.IndiceSeleccionado = -1;
    }

    // ── Selector events ──────────────────────────────────────────────────────

    private void OnFormaPagoCambiada(object? sender, int indice)
    {
        _formaPagoSeleccionada = indice >= 0 && indice < FormasPago.Count ? FormasPago[indice] : null;
        if (_formaPagoSeleccionada is not null)
            Preferences.Default.Set(PrefFormaPago, _formaPagoSeleccionada.Codigo);

        var tarjetas = AppState.Instance.Tarjetas ?? [];
        SelectorTarjeta.Elementos = tarjetas.Select(t => t.DisplayLabel).ToList();
        _tarjetaSeleccionada = null;
        SelectorTarjeta.IndiceSeleccionado = -1;
        OnPropertyChanged(nameof(MostrarTarjetas));
        OnPropertyChanged(nameof(MostrarSelectorTarjeta));
        OnPropertyChanged(nameof(MostrarBotonAgregarTarjeta));

        if (MostrarTarjetas)
        {
            // Restaurar la tarjeta guardada si aplica para este tipo de pago
            var tarjetaId = Preferences.Default.Get(PrefTarjeta, string.Empty);
            if (!string.IsNullOrEmpty(tarjetaId))
            {
                var tIdx = tarjetas.FindIndex(t => t.Id == tarjetaId);
                if (tIdx >= 0)
                {
                    _tarjetaSeleccionada = tarjetas[tIdx];
                    SelectorTarjeta.IndiceSeleccionado = tIdx;
                }
            }
        }
        else
        {
            // Forma de pago no requiere tarjeta: borrar la preferencia guardada
            Preferences.Default.Remove(PrefTarjeta);
        }

        OnPropertyChanged(nameof(PuedeEnviar));
    }

    private void OnTarjetaCambiada(object? sender, int indice)
    {
        var tarjetas = AppState.Instance.Tarjetas ?? [];
        _tarjetaSeleccionada = indice >= 0 && indice < tarjetas.Count ? tarjetas[indice] : null;
        if (_tarjetaSeleccionada is not null)
            Preferences.Default.Set(PrefTarjeta, _tarjetaSeleccionada.Id);
        OnPropertyChanged(nameof(PuedeEnviar));
    }

    private void OnUsoCfdiCambiado(object? sender, int indice)
    {
        _usoCfdiSeleccionado = indice >= 0 && indice < _usoCfdiOpciones.Count ? _usoCfdiOpciones[indice] : null;
        if (_usoCfdiSeleccionado is not null)
            Preferences.Default.Set(PrefUsoCfdi, _usoCfdiSeleccionado.Codigo);
        OnPropertyChanged(nameof(PuedeEnviar));
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private async Task TomarFotoAsync()
    {
        var fileName = await _servicioCamara.TomarFotoAsync();
        _logs.Log($"[PaginaCaptura] TomarFoto — fileName obtenido: '{fileName}'");
        if (string.IsNullOrEmpty(fileName)) return;

        var captura = new CapturaLote { TipoCaptura = TipoCaptura, FileName = fileName };
        _logs.Log($"[PaginaCaptura] TomarFoto — path resuelto: '{captura.Path}' | existe={File.Exists(captura.Path)}");
        _capturas.Add(captura);
        AppState.Instance.CapturasLote = [.. _capturas];
        _logs.Log($"[PaginaCaptura] TomarFoto — AppState actualizado, total capturas={AppState.Instance.CapturasLote?.Count}");
    }

    private async Task EliminarCapturaAsync(CapturaLote captura)
    {
        bool confirmar = await _servicioAlerta.MostrarAsync(
            "Eliminar captura",
            "¿Deseas eliminar esta captura del lote?",
            confirmarText: "Eliminar",
            cancelarText: "Cancelar");

        if (!confirmar) return;

        _capturas.Remove(captura);
        AppState.Instance.CapturasLote = [.. _capturas];
    }

    private async Task VerImagenAsync(CapturaLote captura)
        => await Shell.Current.GoToAsync(nameof(VisorImagenPage),
               new Dictionary<string, object> { ["path"] = captura.Path });

    private async Task EnviarAsync()
    {
        // ── Punto 1: Validar campos obligatorios ─────────────────────────────
        var cuentaFiscal = AppState.Instance.CuentaFiscalActual;
        if (cuentaFiscal is null)
        {
            await _toastService.ShowAsync("Selecciona una cuenta fiscal.", ToastType.Warning, position: ToastPosition.Bottom);
            return;
        }

        var idxFP = SelectorFormaPago.IndiceSeleccionado;
        var formaPago = idxFP >= 0 && idxFP < FormasPago.Count ? FormasPago[idxFP] : null;
        if (formaPago is null)
        {
            await _toastService.ShowAsync("Selecciona el método de pago.", ToastType.Warning, position: ToastPosition.Bottom);
            return;
        }

        var requiereTarjeta = formaPago.Codigo is "4" or "28";
        var tarjetas = AppState.Instance.Tarjetas ?? [];
        var idxT = SelectorTarjeta.IndiceSeleccionado;
        var tarjeta = requiereTarjeta && idxT >= 0 && idxT < tarjetas.Count ? tarjetas[idxT] : null;
        if (requiereTarjeta && tarjeta is null)
        {
            await _toastService.ShowAsync("Selecciona la tarjeta.", ToastType.Warning, position: ToastPosition.Bottom);
            return;
        }

        var idxUso = SelectorUsoCfdi.IndiceSeleccionado;
        var usoCfdi = idxUso >= 0 && idxUso < _usoCfdiOpciones.Count ? _usoCfdiOpciones[idxUso] : null;
        if (usoCfdi is null)
        {
            await _toastService.ShowAsync("Selecciona el uso de CFDI.", ToastType.Warning, position: ToastPosition.Bottom);
            return;
        }

        // ── Punto 2: Validar créditos disponibles en AppState ────────────────
        var creditosAppState = (AppState.Instance.Licenciamiento?.CreditosCaptura ?? 0) -
                               (AppState.Instance.Licenciamiento?.CreditosCapturaConsumo ?? 0);
        if (creditosAppState <= 0)
        {
            await _toastService.ShowAsync("No tienes créditos suficientes.", ToastType.Error, position: ToastPosition.Bottom);
            return;
        }

        // ── Punto 0: Mostrar overlay ─────────────────────────────────────────
        EstaEnviando     = true;
        EnviandoProgreso = 0;

        // loteId: se establece en cuanto el servidor crea el lote.
        // canceladoPorUsuario: true cuando el usuario rechaza el diálogo de créditos insuficientes;
        //   en ese caso NO se llama a Completar — el proceso termina sin más.
        long? loteId              = null;
        bool exitoso              = false;
        bool canceladoPorUsuario  = false;

        try
        {
            // ── Punto 3a: Crear el lote en el servidor ───────────────────────
            var loteRequest = new CreaLoteCaptura
            {
                CuentaFiscalId = cuentaFiscal.CuentaFiscalId,
                Tipo = TipoCaptura,
                ClaveUsoCfdi = usoCfdi.Codigo,
                ClaveFormaPago = formaPago.Codigo,
                TerminacionMedioPago = tarjeta?.UltimosDigitos ?? string.Empty,
                Comentario = NotasAdicionales,
                DesglosarIEPS = DesglosarIeps
            };

            var loteResult = await _servicioTranscript.CrearLoteAsync(loteRequest);
            if (!loteResult.Ok)
            {
                // Sin loteId → no hay lote que completar
                if (loteResult.Error?.HttpCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    await _toastService.ShowAsync("No cuentas con créditos suficientes.", ToastType.Error, position: ToastPosition.Bottom);
                }
                else
                {
                    await _toastService.ShowAsync("Ha ocurrido un error. Inténtalo de nuevo más tarde.", ToastType.Error, position: ToastPosition.Bottom);
                }
            }
            else
            {
                loteId = loteResult.Payload!.Id;
                await AnimarProgresoAsync(0.2f);

                // ── Punto 3b: Obtener precarga (SAS token + créditos del lote)
                var precargaResult = await _servicioTranscript.ObtenerPrecargaAsync(loteId.Value);
                if (!precargaResult.Ok)
                {
                    await _toastService.ShowAsync("Ha ocurrido un error. Inténtalo de nuevo más tarde.", ToastType.Error, position: ToastPosition.Bottom);
                }
                else
                {
                    await AnimarProgresoAsync(0.4f);

                    // ── Punto 4: Validar créditos disponibles vs. capturas ───
                    var creditosDisponibles = precargaResult.Payload!.CreditosDisponibles;
                    var totalCapturas = _capturas.Count;
                    var cantidadAEnviar = totalCapturas;
                    bool continuar = true;

                    if (creditosDisponibles < totalCapturas)
                    {
                        bool aceptar = await _servicioAlerta.MostrarAsync(
                            $"Solo tienes {creditosDisponibles} créditos disponibles.",
                            $"¿Quieres procesar {creditosDisponibles} de las {totalCapturas} capturas solicitadas en este momento?",
                            confirmarText: "Enviar",
                            cancelarText: "Cancelar");

                        if (!aceptar)
                        {
                            canceladoPorUsuario = true;
                            continuar = false;
                        }
                        else
                            cantidadAEnviar = (int)creditosDisponibles;
                    }

                    if (continuar)
                    {
                        // ── Punto 5: Subir archivos al Blob Storage ──────────
                        var rutas = _capturas
                            .Take(cantidadAEnviar)
                            .Select(c => c.Path)
                            .ToList();

                        var progresoBlobCallback = new Progress<double>(p =>
                        {
                            EnviandoProgreso = 0.4 + 0.6 * p;
                            _ = AnimarProgresoAsync((float)EnviandoProgreso);
                        });

                        var subirResult = await _servicioTranscript.SubirArchivosBlobAsync(
                            precargaResult.Payload.SasToken, rutas, progresoBlobCallback);

                        if (!subirResult.Ok)
                            await _toastService.ShowAsync("Ha ocurrido un error al intentar enviar su captura.", ToastType.Error, position: ToastPosition.Bottom);
                        else
                            exitoso = true;
                    }
                }
            }
        }
        catch
        {
            await _toastService.ShowAsync("Ha ocurrido un error. Inténtalo de nuevo más tarde.", ToastType.Error, position: ToastPosition.Bottom);
        }

        // ── Punto 6: Completar el lote ───────────────────────────────────────
        // Se llama sólo si el lote fue creado Y el usuario no canceló en el diálogo de créditos.
        if (loteId.HasValue && !canceladoPorUsuario)
        {
            var completarResult = await _servicioTranscript.CompletarLoteAsync(loteId.Value);
            if (completarResult.Ok && exitoso)
            {
                await Task.Delay(400); // Pausa breve para ver el 100 %
                AppState.Instance.CapturasLote = null;
                _capturas.Clear();
                await _servicioSesion.GetLicenciaAsync();
                await _toastService.ShowAsync("¡Envío completado!", ToastType.Success, position: ToastPosition.Bottom);
                FacturacionPage.PendienteActualizarFacturas = true;
                DashboardPage.PendienteActualizar = true;
                await Shell.Current.GoToAsync("..");
            }
            // En cualquier otro caso: terminar proceso sin eliminar capturas
        }

        EstaEnviando = false;
    }

    private async Task CancelarAsync()
        => await Shell.Current.GoToAsync("..");

    // ── Drawable: círculo de progreso ─────────────────────────────────────────

    private sealed class CircularProgressDrawable : IDrawable
    {
        public float Progress { get; set; }   // 0.0 – 1.0

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var cx = dirtyRect.Width  / 2f;
            var cy = dirtyRect.Height / 2f;
            const float strokeWidth = 14f;
            var radius = Math.Min(cx, cy) - strokeWidth / 2f;

            // Pista (fondo del círculo)
            canvas.StrokeColor = UIHelpers.GetColor("SecondaryBackground");
            canvas.StrokeSize  = strokeWidth;
            canvas.DrawCircle(cx, cy, radius);

            if (Progress <= 0) return;

            // Arco de progreso: arranca en las 12 en punto (270°) y gira a la derecha
            canvas.StrokeColor   = UIHelpers.GetColor("Primary");
            canvas.StrokeSize    = strokeWidth;
            canvas.StrokeLineCap = LineCap.Round;

            var sweep = Progress * 360f;
            canvas.DrawArc(cx - radius, cy - radius, radius * 2f, radius * 2f,
                           270f, 270f + sweep, false, false);
        }
    }

}
