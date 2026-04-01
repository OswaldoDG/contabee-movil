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
    public FormaPago? FormaPagoSeleccionada
    {
        get => _formaPagoSeleccionada;
        set
        {
            _formaPagoSeleccionada = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MostrarTarjetas));
            OnPropertyChanged(nameof(MostrarBotonAgregarTarjeta));
        }
    }

    public bool MostrarTarjetas          => FormaPagoSeleccionada?.Codigo is "4" or "24";
    public bool MostrarBotonAgregarTarjeta => MostrarTarjetas && (AppState.Instance.Tarjetas?.Count ?? 0) == 0;

    // ── Tarjetas ─────────────────────────────────────────────────────────────

    public List<TarjetaModel> Tarjetas => AppState.Instance.Tarjetas ?? [];

    private TarjetaModel? _tarjetaSeleccionada;
    public TarjetaModel? TarjetaSeleccionada
    {
        get => _tarjetaSeleccionada;
        set { _tarjetaSeleccionada = value; OnPropertyChanged(); }
    }

    // ── Uso CFDI ─────────────────────────────────────────────────────────────

    private List<UsoCfdi> _usoCfdiOpciones = [];
    public List<UsoCfdi> UsoCfdiOpciones
    {
        get => _usoCfdiOpciones;
        private set { _usoCfdiOpciones = value; OnPropertyChanged(); }
    }

    private UsoCfdi? _usoCfdiSeleccionado;
    public UsoCfdi? UsoCfdiSeleccionado
    {
        get => _usoCfdiSeleccionado;
        set { _usoCfdiSeleccionado = value; OnPropertyChanged(); }
    }

    // ── Desglosar IEPS ───────────────────────────────────────────────────────

    private bool _desglosarIeps;
    public bool DesglosarIeps
    {
        get => _desglosarIeps;
        set { _desglosarIeps = value; OnPropertyChanged(); }
    }

    // ── Notas adicionales ────────────────────────────────────────────────────

    private string _notasAdicionales = string.Empty;
    public string NotasAdicionales
    {
        get => _notasAdicionales;
        set { _notasAdicionales = value; OnPropertyChanged(); }
    }

    // ── Capturas ─────────────────────────────────────────────────────────────

    private readonly ObservableCollection<CapturaLote> _capturas;
    public ObservableCollection<CapturaLote> Capturas => _capturas;

    public bool TieneCapturas    => _capturas.Count > 0;
    public int  ColumnSpanCamara => TieneCapturas ? 1 : 2;

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

    private void RefrescarTarjetas()
    {
        OnPropertyChanged(nameof(Tarjetas));
        OnPropertyChanged(nameof(MostrarBotonAgregarTarjeta));
    }

    private void ActualizarUsoCfdi()
    {
        var regimen = AppState.Instance.CuentaFiscalActual?.ClaveRegimenFiscal;
        UsoCfdiOpciones    = UsoCfdiProvider.GetUsoCfdi(regimen);
        UsoCfdiSeleccionado = null;
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
        if (FormaPagoSeleccionada is null)
        {
            await _toastService.ShowAsync("Selecciona el medio de pago.", ToastType.Warning);
            return;
        }
        if (MostrarTarjetas && TarjetaSeleccionada is null)
        {
            await _toastService.ShowAsync("Selecciona la tarjeta.", ToastType.Warning);
            return;
        }
        if (UsoCfdiSeleccionado is null)
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

    private void CapturasCarousel_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        if (BindingContext is PaginaCaptura vm && vm.Capturas.Count > 0)
        {
            int index = vm.Capturas.IndexOf(e.CurrentItem as CapturaLote) + 1;
            CapturaCounterLabel.Text = $"{index}/{vm.Capturas.Count}";
        }
        else
        {
            CapturaCounterLabel.Text = "0/0";
        }
    }

    private CapturaLote _capturaSeleccionada;
    public CapturaLote CapturaSeleccionada
    {
        get => _capturaSeleccionada;
        set
        {
            if (_capturaSeleccionada != value)
            {
                _capturaSeleccionada = value;
                OnPropertyChanged();
                CapturaSeleccionadaIndice = Capturas.IndexOf(value) + 1;
            }
        }
    }

    private int _capturaSeleccionadaIndice;
    public int CapturaSeleccionadaIndice
    {
        get => _capturaSeleccionadaIndice;
        set
        {
            if (_capturaSeleccionadaIndice != value)
            {
                _capturaSeleccionadaIndice = value;
                OnPropertyChanged();
            }
        }
    }
}
