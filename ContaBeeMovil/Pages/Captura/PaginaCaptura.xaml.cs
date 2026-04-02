using System.Collections.ObjectModel;
using System.Windows.Input;
using Contabee.Api.Transcript;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Models;
using ContaBeeMovil.Pages.Perfil;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Camara;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Captura;

public partial class PaginaCaptura : ContentPage, IQueryAttributable
{
    private readonly IServicioCamara _servicioCamara;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IToastService _toastService;

    // ── Preferencias recordadas ──────────────────────────────────────────────

    private const string PrefFormaPago = "captura_forma_pago";
    private const string PrefTarjeta   = "captura_tarjeta_id";
    private const string PrefUsoCfdi   = "captura_uso_cfdi";
    private const string PrefDesgIeps  = "captura_desg_ieps";
    private const string PrefNotas     = "captura_notas";

    // ── Constructor ──────────────────────────────────────────────────────────

    public PaginaCaptura(IServicioCamara servicioCamara, IServicioAlerta servicioAlerta, IToastService toastService)
    {
        _servicioCamara = servicioCamara;
        _servicioAlerta = servicioAlerta;
        _toastService   = toastService;

        FormasPago = FormaPagoProvider.GetFormasPago();
        _capturas  = new ObservableCollection<CapturaLote>(AppState.Instance.CapturasLote ?? []);
        _capturas.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TieneCapturas));
            OnPropertyChanged(nameof(ColumnSpanCamara));
        };
        ActualizarUsoCfdi();

        TomarFotoCommand        = new Command(async () => await TomarFotoAsync());
        EliminarCapturaCommand  = new Command<CapturaLote>(async c => await EliminarCapturaAsync(c));
        VerImagenCommand        = new Command<CapturaLote>(async c => await VerImagenAsync(c));
        EnviarCommand           = new Command(async () => await EnviarAsync());
        CancelarCommand         = new Command(async () => await CancelarAsync());
        IrAgregarTarjetaCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(TarjetasPage)));

        InitializeComponent();

        SelectorFormaPago.Elementos = FormasPago.Select(f => f.Descripcion).ToList();
        SelectorFormaPago.IndiceCambiado += OnFormaPagoCambiada;
        SelectorTarjeta.IndiceCambiado   += OnTarjetaCambiada;
        SelectorUsoCfdi.Elementos = _usoCfdiOpciones.Select(u => u.Descripcion).ToList();
        SelectorUsoCfdi.IndiceCambiado   += OnUsoCfdiCambiado;

        RestaurarPreferencias();

        BindingContext = this;
    }

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("tipo", out var t) && t is TipoProcesoCaptura tipo)
            TipoCaptura = tipo;

        _capturas.Clear();
        foreach (var c in AppState.Instance.CapturasLote ?? [])
            _capturas.Add(c);

        ActualizarUsoCfdi();
        var x = TipoCaptura;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
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

    public bool MostrarTarjetas          => _formaPagoSeleccionada?.Codigo is "4" or "24";
    public bool MostrarSelectorTarjeta   => MostrarTarjetas && (AppState.Instance.Tarjetas?.Count ?? 0) > 0;
    public bool MostrarBotonAgregarTarjeta => MostrarTarjetas && (AppState.Instance.Tarjetas?.Count ?? 0) == 0;

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
        private set { _estaEnviando = value; OnPropertyChanged(); }
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
    }

    private void OnTarjetaCambiada(object? sender, int indice)
    {
        var tarjetas = AppState.Instance.Tarjetas ?? [];
        _tarjetaSeleccionada = indice >= 0 && indice < tarjetas.Count ? tarjetas[indice] : null;
        if (_tarjetaSeleccionada is not null)
            Preferences.Default.Set(PrefTarjeta, _tarjetaSeleccionada.Id);
    }

    private void OnUsoCfdiCambiado(object? sender, int indice)
    {
        _usoCfdiSeleccionado = indice >= 0 && indice < _usoCfdiOpciones.Count ? _usoCfdiOpciones[indice] : null;
        if (_usoCfdiSeleccionado is not null)
            Preferences.Default.Set(PrefUsoCfdi, _usoCfdiSeleccionado.Codigo);
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private async Task TomarFotoAsync()
    {
        var path = await _servicioCamara.TomarFotoAsync();
        if (string.IsNullOrEmpty(path)) return;

        _capturas.Add(new CapturaLote { TipoCaptura = TipoCaptura, Path = path });
        AppState.Instance.CapturasLote = [.. _capturas];
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
        // Leer valores directamente de los controles en el momento del envío
        var idxFP = SelectorFormaPago.IndiceSeleccionado;
        var formaPago = idxFP >= 0 && idxFP < FormasPago.Count
            ? FormasPago[idxFP] : null;

        var requiereTarjeta = formaPago?.Codigo is "4" or "24";
        var tarjetas = AppState.Instance.Tarjetas ?? [];
        var idxT = SelectorTarjeta.IndiceSeleccionado;
        var tarjeta = requiereTarjeta && idxT >= 0 && idxT < tarjetas.Count
            ? tarjetas[idxT] : null;

        var idxUso = SelectorUsoCfdi.IndiceSeleccionado;
        var usoCfdi = idxUso >= 0 && idxUso < _usoCfdiOpciones.Count
            ? _usoCfdiOpciones[idxUso] : null;

        if (formaPago is null)
        {
            await _toastService.ShowAsync("Selecciona el medio de pago.", ToastType.Warning);
            return;
        }
        if (requiereTarjeta && tarjeta is null)
        {
            await _toastService.ShowAsync("Selecciona la tarjeta.", ToastType.Warning);
            return;
        }
        if (usoCfdi is null)
        {
            await _toastService.ShowAsync("Selecciona el uso de CFDI.", ToastType.Warning);
            return;
        }

        EstaEnviando    = true;
        EnviandoProgreso = 0;
        try
        {
            var total = _capturas.Count;
            for (int i = 0; i < total; i++)
            {
                // TODO: conectar con IServicioTranscript.EnviarCapturaAsync(...)
                await Task.Delay(600);
                EnviandoProgreso = (double)(i + 1) / total;
            }

            AppState.Instance.CapturasLote = null;
            await _toastService.ShowAsync("Capturas enviadas correctamente.", ToastType.Success);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await _toastService.ShowAsync($"Error al enviar: {ex.Message}", ToastType.Error);
        }
        finally
        {
            EstaEnviando = false;
        }
    }

    private async Task CancelarAsync()
        => await Shell.Current.GoToAsync("..");

}
