using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Contabee.Api.abstractions;
using Contabee.Api.Transcript;
using CommunityToolkit.Maui.Views;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Models;
using ContaBeeMovil.Pages;
using ContaBeeMovil.Pages.Perfil;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Camara;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Documento;
using ContaBeeMovil.Services.Horario;
using ContaBeeMovil.Services.Notifications;
using ContaBeeMovil.Views;
using CommunityToolkit.Maui.Extensions;

namespace ContaBeeMovil.Pages.Captura;

public partial class PaginaCaptura : ContentPage, IQueryAttributable
{
    private readonly IServicioCamara _servicioCamara;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioToast _servicioToast;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioTranscript _servicioTranscript;
    private readonly IServicioProcesadorDocumento _procesadorDocumento;
    private readonly IServicioHorarioCaptura _servicioHorario;
    private readonly IServicioLogs _logs;

    // ── Preferencias recordadas ──────────────────────────────────────────────

    private const string PrefFormaPago = "captura_forma_pago";
    private const string PrefTarjeta   = "captura_tarjeta_id";
    private const string PrefUsoCfdi   = "captura_uso_cfdi";
    private const string PrefDesgIeps  = "captura_desg_ieps";
    private const string PrefNotas     = "captura_notas";
    private const string PrefSoloEvidencia = "captura_solo_evidencia";
    private const string PrefCapturaRemota = "captura_remota";
    private const string PrefUrgente       = "captura_urgente";

    // ── Constructor ──────────────────────────────────────────────────────────

    public PaginaCaptura(IServicioCamara servicioCamara, IServicioAlerta servicioAlerta, IServicioToast servicioToast, IServicioSesion servicioSesion, IServicioTranscript servicioTranscript, IServicioProcesadorDocumento procesadorDocumento, IServicioHorarioCaptura servicioHorario, IServicioLogs logs)
    {
        _servicioCamara    = servicioCamara;
        _servicioAlerta    = servicioAlerta;
        _servicioToast     = servicioToast;
        _servicioSesion    = servicioSesion;
        _servicioTranscript = servicioTranscript;
        _procesadorDocumento = procesadorDocumento;
        _servicioHorario   = servicioHorario;
        _logs              = logs;

        FormasPago = FormaPagoProvider.GetFormasPago()
                                      .OrderBy(f => f.Descripcion, StringComparer.CurrentCultureIgnoreCase)
                                      .ToList();
        _capturas  = [];
        _capturas.CollectionChanged += OnCapturasCollectionChanged;
        ActualizarUsoCfdi();

        TomarFotoCommand        = new Command(async () => await TomarFotoAsync());
        VerImagenCommand        = new Command<CapturaLote>(async c => await VerImagenAsync(c));
        EliminarCapturaCommand  = new Command<CapturaLote>(async c => await EliminarCapturaAsync(c));
        EnviarCommand           = new Command(async () => await EnviarAsync());
        IniciarEnvioCommand     = new Command(async () => await IniciarEnvioAsync());
        CancelarCommand         = new Command(async () => await CancelarAsync());

        InitializeComponent();

        BtnCamaraSin.Text = Fonts.FluentUIFilled.camera_20_filled;
        BtnCamaraSin.FontFamily = Fonts.FluentUIFilled.FontFamily;
        BtnCamaraSin.FontSize = 28;
        BtnCamaraCon.Text = Fonts.FluentUIFilled.camera_20_filled;
        BtnCamaraCon.FontFamily = Fonts.FluentUIFilled.FontFamily;
        BtnCamaraCon.FontSize = 28;

        CircularProgress.Drawable = _progressDrawable;

        SelectorFormaPago.Elementos = FormasPago.Select(f => f.Descripcion).ToList();
        SelectorFormaPago.IndiceCambiado += OnFormaPagoCambiada;
        SelectorTarjeta.IndiceCambiado   += OnTarjetaCambiada;
        SelectorUsoCfdi.Elementos = ConstruirElementosUsoCfdi(_usoCfdiOpciones);
        SelectorUsoCfdi.IndiceCambiado   += OnUsoCfdiCambiado;

        RestaurarPreferencias();

        BindingContext = this;
    }

    // Suscrito/desuscrito en OnAppearing/OnDisappearing: la página es transient y una
    // lambda en el constructor quedaría anclada a AppState.Instance (singleton) para
    // siempre, filtrando cada instancia creada.
    private void OnAppStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.Licenciamiento))
            RefrescarCreditos();
    }

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("tipo", out var t) && t is TipoProcesoCaptura tipo && tipo != _tipoCaptura)
        {
            TipoCaptura = tipo;
            _capturas.Clear();
        }

        _procesoAsociadoId = null;
        if (query.TryGetValue("procesoId", out var procesoObj))
        {
            if (procesoObj is Guid procesoGuid)
                _procesoAsociadoId = procesoGuid;
            else if (procesoObj is string procesoTxt && Guid.TryParse(procesoTxt, out var parsedGuid))
                _procesoAsociadoId = parsedGuid;
        }

        _pendienteVerificarFotos = true;
        ActualizarUsoCfdi();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Shell.TitleView no refresca bindings cuando la página estuvo en segundo plano
        RefrescarCreditos();

        PuedeSerUrgente = DateTime.Now.Hour < 20;
        AppState.Instance.PropertyChanged += OnAppStatePropertyChanged;
        SharedImageHandler.ImagenCompartidaRecibida += OnImagenCompartidaRecibida;

        IniciarSeguimientoHorario();
        ReanudarAvisoHorarioSiTocaba();

        _ = CargarTarjetasYRefrescarAsync();

        if (_pendienteVerificarFotos)
        {
            _pendienteVerificarFotos = false;
            _ = InicializarCapturasAsync();
        }
    }

    private async Task InicializarCapturasAsync()
    {
        if (!_capturas.Any(c => c.TipoCaptura == TipoCaptura))
            await VerificarFotosGuardadasAsync();

        var sharedFileName = SharedImageHandler.TakePendingSharedImage();
        if (string.IsNullOrEmpty(sharedFileName)) return;

        var captura = new CapturaLote { TipoCaptura = TipoCaptura, FileName = sharedFileName, EsCompartida = true };
        _capturas.Insert(0, captura);
        CapturaSeleccionada = captura;
        AppState.Instance.CapturasLote = [.. _capturas];
        OnPropertyChanged(nameof(TieneCapturas));
        NotificarPanelCentral();
        await _servicioToast.MostrarAsync("Imagen agregada correctamente.", ToastIcono.Info, ToastPosicion.Bottom);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        AppState.Instance.PropertyChanged -= OnAppStatePropertyChanged;
        SharedImageHandler.ImagenCompartidaRecibida -= OnImagenCompartidaRecibida;
        _timerHorario?.Stop();

        // Si el aviso seguía en pantalla, no lo cerró el usuario: lo está tapando una
        // navegación temporal (visor de imagen, cámara). Se apunta para volver a sacarlo
        // al regresar — sin esto, abrir una foto lo mataba para siempre, porque la
        // bandera de "una vez por lote" impide que el disparo normal lo repita.
        _avisoHorarioPendienteReanudar = AvisoMascotaHorario.IsVisible;
        AvisoMascotaHorario.OcultarInmediato();
    }

    // ── Horario de captura de ContaBee ───────────────────────────────────────

    private EstadoHorarioCaptura? _estadoHorario;
    private IDispatcherTimer? _timerHorario;

    /// <summary>
    /// El aviso sólo aplica al crédito de <b>Captura</b>: en Autoservicio captura el
    /// propio usuario, así que el horario de ContaBee no lo afecta.
    /// </summary>
    public bool MostrarAvisoHorario => UsarCaptura && FueraDeHorario;

    /// <summary>
    /// Fuera de horario, sin mirar el tipo de crédito. Lo usa el flyout "Quién captura",
    /// que muestra las dos opciones a la vez y necesita el dato en la de Contabee
    /// aunque el crédito activo en ese instante sea el de Autoservicio.
    /// </summary>
    public bool FueraDeHorario => _estadoHorario is { Abierto: false };

    public string MensajeHorario       => _estadoHorario?.Mensaje ?? string.Empty;
    public string ResumenHorario       => _estadoHorario?.ResumenCorto ?? string.Empty;
    public string MensajeBreveHorario  => _estadoHorario?.MensajeBreve ?? string.Empty;

    // El coach mark de la mascota quedó como ÚNICO aviso de horario; el aviso amplio,
    // la franja de estado y el bloque dentro del selector "Quién captura" están
    // desactivados en el XAML (ver los comentarios [DESACTIVADO] de PaginaCaptura.xaml).
    //
    // MostrarAvisoHorarioAmplio se conserva porque es lo que hay que volver a enlazar
    // para revivir aquel aviso; hoy no lo consume ningún binding.
    public bool MostrarAvisoHorarioAmplio => MostrarAvisoHorario && !TieneCapturas;

    // Sin el aviso amplio, el estado vacío vuelve a depender sólo de que no haya fotos:
    // antes se apagaba para cederle el hueco central, y dejarlo así habría dejado la
    // zona de capturas en blanco fuera de horario.
    public bool MostrarEstadoVacio        => !TieneCapturas;

    private void NotificarPanelCentral()
    {
        OnPropertyChanged(nameof(MostrarAvisoHorario));
        OnPropertyChanged(nameof(FueraDeHorario));
        OnPropertyChanged(nameof(MostrarAvisoHorarioAmplio));
        OnPropertyChanged(nameof(MostrarEstadoVacio));
        EvaluarCoachMarkHorario();
    }

    private async void OnAvisoHorarioTapped(object sender, TappedEventArgs e)
    {
        if (!FueraDeHorario) return;
        await AvisoMascotaHorario.MostrarAsync();
    }

    // ── Coach mark de la mascota ─────────────────────────────────────────────
    // NUNCA sale con el lote vacío: sin fotos el aviso hablaría de un envío que el
    // usuario no está haciendo. Da igual de dónde salga la foto —cámara, imagen
    // compartida o las guardadas que se conservan al entrar—, el disparo es el mismo:
    // pasar de 0 fotos a la primera.
    //
    // Se dispara UNA vez por lote. Agregar la 2ª, 3ª… no lo repite (a la tercera foto
    // la animación estorbaría a quien está capturando varios tickets seguidos), y al
    // vaciarse el lote —envío exitoso, o borrar todas las fotos— se rearma para el
    // siguiente.
    //
    // Se engancha a NotificarPanelCentral porque ahí desembocan TODOS los caminos
    // que agregan fotos (cámara, imagen compartida y fotos guardadas pasan por
    // OnCapturasCollectionChanged) además del cambio de crédito activo y el timer
    // del minuto; la bandera es la que garantiza que sólo salga una vez.
    private bool _coachMarkHorarioMostrado;

    private void EvaluarCoachMarkHorario()
    {
        if (!TieneCapturas)
        {
            _coachMarkHorarioMostrado = false;

            // Quedarse sin fotos con el aviso en pantalla —borrar la última, o un envío
            // que salió bien— lo deja hablando de un lote que ya no existe. Se retrae.
            // Hace falta hacerlo a mano porque el aviso NO se va solo: se queda hasta
            // que lo cierren. Y de paso cancela el "volver a sacarlo", que si no lo
            // resucitaría al regresar de la cámara o del visor.
            // Por el hilo: aquí se llega también desde OnImagenCompartidaRecibida, que
            // es un evento de la extensión de compartir, y retraer el aviso es animar.
            _avisoHorarioPendienteReanudar = false;
            MainThread.BeginInvokeOnMainThread(() => _ = AvisoMascotaHorario.OcultarAsync());
            return;
        }

        if (_coachMarkHorarioMostrado || !MostrarAvisoHorario) return;

        _coachMarkHorarioMostrado = true;
        _ = MostrarCoachMarkHorarioAsync();
    }

    private async Task MostrarCoachMarkHorarioAsync()
    {
        // Con fotos guardadas esto cae dentro de OnAppearing: sin la pausa la
        // animación arrancaría mientras la página todavía se está montando y la
        // entrada se vería a tirones o directamente perdida.
        await Task.Delay(450);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Se revalida DESPUÉS de la pausa y no sólo antes: en esos 450 ms el lote
            // pudo vaciarse —borrar la foto recién tomada, o el diálogo de imágenes
            // guardadas terminando en "Eliminar"— y el aviso saldría sobre cero fotos.
            // Se rearma la bandera para que vuelva a dispararse cuando haya de nuevo
            // motivo; si no, este lote se quedaría sin aviso para siempre.
            if (!TieneCapturas || !MostrarAvisoHorario)
            {
                _coachMarkHorarioMostrado = false;
                return Task.CompletedTask;
            }

            // Los límites se fijan ANTES de animar: si la tira entrara con el margen
            // viejo, se vería saltar de sitio en cuanto se recalculara.
            ActualizarMargenAviso();
            return AvisoMascotaHorario.MostrarAsync();
        });
    }

    /// <summary>
    /// El aviso estaba en pantalla cuando la página se fue por una navegación temporal
    /// (visor de imagen, cámara) y hay que devolverlo al volver. Distinto de cerrarlo:
    /// si el usuario lo cerró, <c>IsVisible</c> ya era false y esto queda en false.
    /// </summary>
    private bool _avisoHorarioPendienteReanudar;

    private void ReanudarAvisoHorarioSiTocaba()
    {
        if (!_avisoHorarioPendienteReanudar) return;

        _avisoHorarioPendienteReanudar = false;

        // Se revalida: mientras el usuario estuvo fuera pudo entrar el horario hábil o
        // pudo quedarse sin fotos.
        if (MostrarAvisoHorario && TieneCapturas)
            _ = MostrarCoachMarkHorarioAsync();
    }

    private void IniciarSeguimientoHorario()
    {
        _simIndice = Math.Clamp(Preferences.Default.Get(PrefSimHorario, 0), 0, SimulacionesHorario.Length - 1);
        AplicarSimulacionHorario();
        _ = PrecargarFeriadosAsync();

        // La página puede quedar abierta cuando se cruza el límite de las 9:00 / 18:00,
        // así que se reevalúa cada minuto mientras esté visible.
        if (_timerHorario is null)
        {
            _timerHorario = Dispatcher.CreateTimer();
            _timerHorario.Interval = TimeSpan.FromMinutes(1);
            _timerHorario.Tick += (_, _) => RefrescarHorario();
        }
        _timerHorario.Start();
    }

    private async Task PrecargarFeriadosAsync()
    {
        await _servicioHorario.PrecargarFeriadosAsync();
        MainThread.BeginInvokeOnMainThread(RefrescarHorario);
    }

    private void RefrescarHorario()
    {
        _estadoHorario = _servicioHorario.ObtenerEstado();
        OnPropertyChanged(nameof(MensajeHorario));
        OnPropertyChanged(nameof(ResumenHorario));
        OnPropertyChanged(nameof(MensajeBreveHorario));
        NotificarPanelCentral();
    }

    // ── Simulación de horario (sólo con Modo Desarrollador) ──────────────────
    // Sin esto habría que esperar a que sea de noche o fin de semana para ver el
    // aviso. Con el modo dev activo (10 taps a la versión en Acerca de), cada tap en
    // el título "Captura" recorre estos modos. Para un usuario normal el tap no hace
    // nada y el momento simulado siempre queda en null.

    private const string PrefSimHorario = "dev_horario_simulacion";

    private static readonly (string Etiqueta, Func<DateTime, DateTime?> Momento)[] SimulacionesHorario =
    [
        ("hora real",              _   => null),
        ("sábado 11:00",           hoy => ProximoDiaSemana(hoy, DayOfWeek.Saturday).AddHours(11)),
        ("hoy 21:00",              hoy => hoy.AddHours(21)),
        ("hoy 07:00",              hoy => hoy.AddHours(7)),
        ("último día del mes 11:00", hoy => new DateTime(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month)).AddHours(11)),
    ];

    private int _simIndice;

    private static DateTime ProximoDiaSemana(DateTime desde, DayOfWeek dia)
        => desde.AddDays(((int)dia - (int)desde.DayOfWeek + 7) % 7);

    private async void OnTituloCapturaTapped(object sender, TappedEventArgs e)
    {
        if (!AppState.Instance.EsDev) return;

        _simIndice = (_simIndice + 1) % SimulacionesHorario.Length;
        Preferences.Default.Set(PrefSimHorario, _simIndice);
        AplicarSimulacionHorario();

        await _servicioToast.MostrarAsync($"Horario simulado: {SimulacionesHorario[_simIndice].Etiqueta}",
                                          ToastIcono.Info, ToastPosicion.Bottom);
    }

    private void AplicarSimulacionHorario()
    {
        ServicioHorarioCaptura.MomentoSimuladoCentral = AppState.Instance.EsDev
            ? SimulacionesHorario[_simIndice].Momento(DateTime.Today)
            : null;
        RefrescarHorario();
    }

    protected override bool OnBackButtonPressed()
    {
        MainThread.BeginInvokeOnMainThread(async () => await CancelarAsync());
        return true;
    }

    private void OnImagenCompartidaRecibida(string fileName)
    {
        var captura = new CapturaLote { TipoCaptura = TipoCaptura, FileName = fileName };
        _capturas.Insert(0, captura);
        CapturaSeleccionada = captura;
        AppState.Instance.CapturasLote = [.. _capturas];
        OnPropertyChanged(nameof(TieneCapturas));
        NotificarPanelCentral();
        _ = _servicioToast.MostrarAsync("Imagen agregada correctamente.", ToastIcono.Info, ToastPosicion.Bottom);
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
        capturasGuardadas = capturasGuardadas
            .OrderByDescending(c => File.GetLastWriteTimeUtc(c.Path))
            .ToList();
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

            if (_capturas.Count > 0)
                CapturaSeleccionada = _capturas[0];
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
        // Pinta el selector de inmediato con lo que ya esté en caché (AppState),
        // así no aparece vacío mientras la red responde en conexiones lentas.
        RefrescarTarjetas();

        // Si aún no hay tarjetas cargadas, tráelas y vuelve a refrescar al terminar.
        if (AppState.Instance.Tarjetas is null)
        {
            await _servicioSesion.GetTarjetasAsync();
            RefrescarTarjetas();
        }

        // La licencia/créditos es independiente del selector de tarjetas: se pide
        // al final para no bloquear su render.
        await _servicioSesion.GetLicenciaAsync();
    }

    // ── Parámetro de navegación ──────────────────────────────────────────────

    private TipoProcesoCaptura _tipoCaptura;
    private Guid? _procesoAsociadoId;

    public TipoProcesoCaptura TipoCaptura
    {
        get => _tipoCaptura;
        set { _tipoCaptura = value; OnPropertyChanged(); }
    }

    // ── Forma de Pago ────────────────────────────────────────────────────────

    public List<FormaPago> FormasPago { get; }

    private FormaPago? _formaPagoSeleccionada;

    public bool MostrarTarjetas          => _formaPagoSeleccionada?.Codigo is "4" or "28";
    // El selector siempre se muestra cuando la forma de pago requiere tarjeta:
    // incluye "Sin tarjeta" como primera opción además de las tarjetas registradas.
    public bool MostrarSelectorTarjeta   => MostrarTarjetas;

    // La tarjeta ya no es obligatoria: el usuario puede elegir "Sin tarjeta".
    public bool PuedeEnviar =>
        TieneCapturas &&
        _formaPagoSeleccionada is not null &&
        _usoCfdiSeleccionado is not null;

    // ── Evidencias ───────────────────────────────────────────────────────────

    private bool _soloEvidencia;
    public bool SoloEvidencia
    {
        get => _soloEvidencia;
        set
        {
            if (_soloEvidencia == value) return;
            _soloEvidencia = value;
            Preferences.Default.Set(PrefSoloEvidencia, value);

            if (!value)
                CapturaRemota = false;

            OnPropertyChanged();
            OnPropertyChanged(nameof(MostrarCapturaRemota));
            OnPropertyChanged(nameof(MostrarMontoTicketInput));
            OnPropertyChanged(nameof(CapturaItemHeight));
            OnPropertyChanged(nameof(ResumenOpcionesAvanzadas));
        }
    }

    private bool _capturaRemota;
    public bool CapturaRemota
    {
        get => _capturaRemota;
        set
        {
            if (_capturaRemota == value) return;
            _capturaRemota = value;
            Preferences.Default.Set(PrefCapturaRemota, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(MostrarMontoTicketInput));
            OnPropertyChanged(nameof(CapturaItemHeight));
        }
    }

    private bool _esUrgente;
    public bool EsUrgente
    {
        get => _esUrgente;
        set
        {
            if (_esUrgente == value) return;
            _esUrgente = value;
            Preferences.Default.Set(PrefUrgente, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResumenOpcionesAvanzadas));
        }
    }

    private bool _puedeSerUrgente;
    public bool PuedeSerUrgente
    {
        get => _puedeSerUrgente;
        private set
        {
            _puedeSerUrgente = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OpacidadUrgente));
        }
    }

    // El chip no es un CheckBox, así que no hereda el atenuado de IsEnabled: se hace
    // a mano. El tap ya lo bloquea OnEsUrgenteTapped.
    public double OpacidadUrgente => PuedeSerUrgente ? 1.0 : 0.4;

    public bool MostrarCapturaRemota   => SoloEvidencia;
    public bool MostrarMontoTicketInput => SoloEvidencia && !CapturaRemota;

    // ── "Más opciones" (sección colapsable) ──────────────────────────────────
    // Medio de pago, tarjeta y uso CFDI se usan en cada captura y quedan siempre a la
    // vista. El resto se colapsa: en pantallas chicas esas cuatro filas dejaban el área
    // de fotos inservible.

    private const string PrefMasOpciones = "captura_mas_opciones";

    private bool _opcionesAvanzadasVisibles;
    public bool OpcionesAvanzadasVisibles
    {
        get => _opcionesAvanzadasVisibles;
        set
        {
            if (_opcionesAvanzadasVisibles == value) return;
            _opcionesAvanzadasVisibles = value;
            Preferences.Default.Set(PrefMasOpciones, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IconoMasOpciones));
            OnPropertyChanged(nameof(ResumenOpcionesAvanzadas));
        }
    }

    public string IconoMasOpciones => OpcionesAvanzadasVisibles
        ? Fonts.FluentUI.chevron_down_20_regular
        : Fonts.FluentUI.chevron_right_20_regular;

    /// <summary>
    /// Qué opciones avanzadas quedaron activas, para que no se escondan al colapsar la
    /// sección — cambian el CFDI y el usuario tiene que poder verlas de un vistazo.
    /// Vacío cuando la sección está abierta (ahí ya se ven los checkboxes).
    /// </summary>
    public string ResumenOpcionesAvanzadas
    {
        get
        {
            if (OpcionesAvanzadasVisibles) return string.Empty;

            var activas = new List<string>(4);
            if (SoloEvidencia)                              activas.Add("Evidencia");
            if (EsUrgente)                                  activas.Add("Urgente");
            if (DesglosarIeps)                              activas.Add("IEPS");
            if (!string.IsNullOrWhiteSpace(NotasAdicionales)) activas.Add("Notas");

            return string.Join(" · ", activas);
        }
    }

    private void OnMasOpcionesTapped(object sender, TappedEventArgs e)
        => OpcionesAvanzadasVisibles = !OpcionesAvanzadasVisibles;

    // ── Tarjetas ─────────────────────────────────────────────────────────────

    private const string SinTarjetaLabel = "Sin especificar tarjeta";

    private TarjetaModel? _tarjetaSeleccionada;

    // Tarjetas ordenadas alfabéticamente por su etiqueta. Los índices del dropdown
    // se resuelven contra esta lista, así que SIEMPRE hay que usar este helper en
    // lugar de AppState.Instance.Tarjetas directamente.
    private static List<TarjetaModel> TarjetasOrdenadas()
        => (AppState.Instance.Tarjetas ?? [])
           .OrderBy(t => t.DisplayLabel, StringComparer.CurrentCultureIgnoreCase)
           .ToList();

    // El dropdown de tarjetas lleva "Sin tarjeta" en el índice 0 y las tarjetas
    // registradas a partir del índice 1.
    private static List<string> ConstruirElementosTarjeta(IReadOnlyList<TarjetaModel> tarjetas)
    {
        var elementos = new List<string> { SinTarjetaLabel };
        elementos.AddRange(tarjetas.Select(t => t.DisplayLabel));
        return elementos;
    }

    // Índice del dropdown → tarjeta (índice 0 = "Sin tarjeta" → null).
    private static TarjetaModel? TarjetaDesdeIndice(IReadOnlyList<TarjetaModel> tarjetas, int indice)
        => indice >= 1 && indice - 1 < tarjetas.Count ? tarjetas[indice - 1] : null;

    // ── Uso CFDI ─────────────────────────────────────────────────────────────

    private List<UsoCfdi> _usoCfdiOpciones = [];
    private UsoCfdi? _usoCfdiSeleccionado;

    // El catálogo c_UsoCFDI del SAT trae las descripciones con punto final; el de
    // formas de pago no. Se recorta sólo para mostrar — el catálogo queda intacto.
    private static List<string> ConstruirElementosUsoCfdi(IEnumerable<UsoCfdi> opciones)
        => opciones.Select(u => u.Descripcion.TrimEnd('.')).ToList();

    // ── Desglosar IEPS ───────────────────────────────────────────────────────

    private bool _desglosarIeps;
    public bool DesglosarIeps
    {
        get => _desglosarIeps;
        set
        {
            _desglosarIeps = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResumenOpcionesAvanzadas));
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
            OnPropertyChanged(nameof(ResumenOpcionesAvanzadas));
            Preferences.Default.Set(PrefNotas, value);
        }
    }

    // ── Capturas ─────────────────────────────────────────────────────────────

    private readonly ObservableCollection<CapturaLote> _capturas;

    public ObservableCollection<CapturaLote> Capturas => _capturas;

    private CapturaLote? _capturaSeleccionada;
    public CapturaLote? CapturaSeleccionada
    {
        get => _capturaSeleccionada;
        set
        {
            _capturaSeleccionada = value;
            OnPropertyChanged();
        }
    }

    public bool TieneCapturas    => _capturas.Count > 0;
    public int  ColumnSpanCamara => TieneCapturas ? 1 : 2;
    public int  CreditosCaptura  => AppState.Instance.Licenciamiento?.CreditosDisponibles ?? 0;

    // ── Tipo de crédito de captura (Captura vs Autoservicio) ─────────────────

    public int  CreditosAutoservicio      => AppState.Instance.Licenciamiento?.CreditosAutoDisponibles ?? 0;
    public bool TieneCreditosCaptura      => CreditosCaptura > 0;
    public bool TieneCreditosAutoservicio => CreditosAutoservicio > 0;
    public bool SoloCaptura               => TieneCreditosCaptura && !TieneCreditosAutoservicio;
    public bool SoloAutoservicio          => !TieneCreditosCaptura && TieneCreditosAutoservicio;
    public bool TieneAmbosCreditos        => TieneCreditosCaptura && TieneCreditosAutoservicio;
    public bool SinCreditos               => !TieneCreditosCaptura && !TieneCreditosAutoservicio;

    private void RefrescarCreditos()
    {
        OnPropertyChanged(nameof(CreditosCaptura));
        OnPropertyChanged(nameof(CreditosAutoservicio));
        OnPropertyChanged(nameof(TieneCreditosCaptura));
        OnPropertyChanged(nameof(TieneCreditosAutoservicio));
        OnPropertyChanged(nameof(SoloCaptura));
        OnPropertyChanged(nameof(SoloAutoservicio));
        OnPropertyChanged(nameof(TieneAmbosCreditos));
        OnPropertyChanged(nameof(SinCreditos));
        OnPropertyChanged(nameof(CreditosActivos));

        if (TieneCreditosCaptura)        // SoloCaptura o Ambos → captura por defecto
            UsarAutoservicio = false;
        else                             // SoloAutoservicio o SinCreditos
            UsarAutoservicio = true;
    }

    private bool _usarAutoservicio;
    public bool UsarAutoservicio
    {
        get => _usarAutoservicio;
        set
        {
            if (_usarAutoservicio == value) return;
            _usarAutoservicio = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UsarCaptura));
            OnPropertyChanged(nameof(TipoCreditoActivo));
            OnPropertyChanged(nameof(CreditosActivos));
            OnPropertyChanged(nameof(ColorCreditoActivo));
            OnPropertyChanged(nameof(ColorContrasteCredito));
            OnPropertyChanged(nameof(BrushContrasteCredito));
            NotificarPanelCentral();
        }
    }
    public bool UsarCaptura => !UsarAutoservicio;

    // Tipo y número del crédito activo (separados para darles tamaños distintos), y su color
    public string TipoCreditoActivo => UsarAutoservicio ? "Auto" : "Captura";
    public int    CreditosActivos   => UsarAutoservicio ? CreditosAutoservicio : CreditosCaptura;
    public Color ColorCreditoActivo => UsarAutoservicio
        ? UIHelpers.GetColor("Auto")
        : UIHelpers.GetColor("Captura");
    // Color de contraste para el ícono sobre el fondo del color activo
    public Color ColorContrasteCredito => UsarAutoservicio
        ? Colors.White
        : UIHelpers.GetColor("OnPrimary");
    // Mismo color como Brush, para el trazo del ícono vectorial (Path.Stroke)
    public Brush BrushContrasteCredito => new SolidColorBrush(ColorContrasteCredito);

    // ── Ancho dinámico de cada card en el carrusel ───────────────────────────

    private double _capturaItemWidth = 300;
    public double CapturaItemWidth
    {
        get => _capturaItemWidth;
        private set
        {
            _capturaItemWidth = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CapturaItemHeight));
        }
    }

    private double _alturaZonaCapturas;

    /// <summary>
    /// Alto ideal de la tarjeta según su ancho, pero nunca mayor que el espacio que
    /// realmente le quedó al carrusel. Antes sólo dependía del ancho, así que en
    /// pantallas cortas con "Más opciones" abierto la tarjeta pedía más alto del
    /// disponible y se recortaba por arriba — se perdía el botón de eliminar.
    /// </summary>
    public double CapturaItemHeight
    {
        get
        {
            var ideal = _capturaItemWidth * (MostrarMontoTicketInput ? 1.08 : 1.22);
            return _alturaZonaCapturas > 0 ? Math.Min(ideal, _alturaZonaCapturas) : ideal;
        }
    }

    private void OnZonaCapturasSizeChanged(object? sender, EventArgs e)
    {
        // Fuera del guard de abajo a propósito: la POSICIÓN de la zona de fotos cambia
        // al desplegar "Más opciones" aunque su alto acabe igual, y de esa posición
        // depende que la tira no tape los formularios.
        ActualizarMargenAviso();

        var alto = ZonaCapturas.Height;
        // El umbral evita el bucle SizeChanged → relayout → SizeChanged.
        if (alto <= 0 || Math.Abs(_alturaZonaCapturas - alto) < 0.5) return;

        _alturaZonaCapturas = alto;
        OnPropertyChanged(nameof(CapturaItemHeight));
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0)
            CapturaItemWidth = width - 24; // 12 px de margen a cada lado

        // La fila de controles es Auto: sin tope se queda con todo el alto que pida su
        // contenido y el carrusel se queda con las sobras. Con el tope, lo que no cabe
        // se scrollea dentro del propio ScrollView y el área de fotos conserva su mitad.
        if (height > 0)
            ScrollControles.MaximumHeightRequest = height * 0.45;
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
            OnPropertyChanged(nameof(OpacidadCreditos));
            if (value)
            {
                _progressDrawable.Progress = 0f;
                // El arco toma el color del crédito con el que se está enviando:
                // Captura → Primary (amarillo) | Autoservicio → Auto (azul)
                _progressDrawable.ColorArco = UsarAutoservicio
                    ? UIHelpers.GetColor("Auto")
                    : UIHelpers.GetColor("Primary");
                CircularProgress?.Invalidate();
            }
        }
    }

    // Mientras se envía, el badge de créditos se atenúa y deja de ser interactivo.
    public double OpacidadCreditos => EstaEnviando ? 0.4 : 1.0;

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
    public ICommand VerImagenCommand        { get; }
    public ICommand EliminarCapturaCommand  { get; }
    public ICommand EnviarCommand           { get; }
    public ICommand IniciarEnvioCommand     { get; }
    public ICommand CancelarCommand         { get; }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void RestaurarPreferencias()
    {
        // DesglosarIeps (directo al campo para no reescribir la preferencia en el setter)
        _desglosarIeps = Preferences.Default.Get(PrefDesgIeps, false);
        OnPropertyChanged(nameof(DesglosarIeps));

        // Notas adicionales
        _notasAdicionales = Preferences.Default.Get(PrefNotas, string.Empty);
        OnPropertyChanged(nameof(NotasAdicionales));

        _soloEvidencia = Preferences.Default.Get(PrefSoloEvidencia, false);
        OnPropertyChanged(nameof(SoloEvidencia));
        _capturaRemota = false;   // Checkbox oculto — siempre false
        OnPropertyChanged(nameof(CapturaRemota));
        OnPropertyChanged(nameof(MostrarCapturaRemota));
        OnPropertyChanged(nameof(MostrarMontoTicketInput));
        _esUrgente = Preferences.Default.Get(PrefUrgente, false);
        OnPropertyChanged(nameof(EsUrgente));

        // Colapsado por default: es lo que libera el espacio del área de fotos.
        _opcionesAvanzadasVisibles = Preferences.Default.Get(PrefMasOpciones, false);
        OnPropertyChanged(nameof(OpcionesAvanzadasVisibles));
        OnPropertyChanged(nameof(IconoMasOpciones));
        OnPropertyChanged(nameof(ResumenOpcionesAvanzadas));

        // Forma de pago
        var codigoFP = Preferences.Default.Get(PrefFormaPago, string.Empty);
        if (!string.IsNullOrEmpty(codigoFP))
        {
            var idx = FormasPago.FindIndex(f => f.Codigo == codigoFP);
            if (idx >= 0)
            {
                _formaPagoSeleccionada = FormasPago[idx];
                SelectorFormaPago.IndiceSeleccionado = idx;
                var tarjetas = TarjetasOrdenadas();
                SelectorTarjeta.Elementos = ConstruirElementosTarjeta(tarjetas);
                OnPropertyChanged(nameof(MostrarTarjetas));
                OnPropertyChanged(nameof(MostrarSelectorTarjeta));

                // Tarjeta (sólo si la forma de pago seleccionada la requiere).
                // Por defecto "Sin tarjeta" (índice 0); si hay una guardada, la restaura.
                if (MostrarTarjetas)
                {
                    SelectorTarjeta.IndiceSeleccionado = 0;
                    var tarjetaId = Preferences.Default.Get(PrefTarjeta, string.Empty);
                    if (!string.IsNullOrEmpty(tarjetaId))
                    {
                        var tIdx = tarjetas.FindIndex(t => t.Id == tarjetaId);
                        if (tIdx >= 0)
                        {
                            _tarjetaSeleccionada = tarjetas[tIdx];
                            SelectorTarjeta.IndiceSeleccionado = tIdx + 1;
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
        var tarjetas = TarjetasOrdenadas();
        SelectorTarjeta.Elementos = ConstruirElementosTarjeta(tarjetas);

        // Por defecto "Sin tarjeta" (índice 0); si hay una guardada, mantenerla.
        _tarjetaSeleccionada = null;
        SelectorTarjeta.IndiceSeleccionado = MostrarTarjetas ? 0 : -1;
        if (MostrarTarjetas)
        {
            var tarjetaId = Preferences.Default.Get(PrefTarjeta, string.Empty);
            if (!string.IsNullOrEmpty(tarjetaId))
            {
                var idx = tarjetas.FindIndex(t => t.Id == tarjetaId);
                if (idx >= 0)
                {
                    _tarjetaSeleccionada = tarjetas[idx];
                    SelectorTarjeta.IndiceSeleccionado = idx + 1;
                }
            }
        }

        OnPropertyChanged(nameof(MostrarSelectorTarjeta));
        OnPropertyChanged(nameof(PuedeEnviar));
    }

    private void ActualizarUsoCfdi()
    {
        var regimen = AppState.Instance.CuentaFiscalActual?.ClaveRegimenFiscal;
        _usoCfdiOpciones    = UsoCfdiProvider.GetUsoCfdi(regimen)
                                             .OrderBy(u => u.Descripcion, StringComparer.CurrentCultureIgnoreCase)
                                             .ToList();
        _usoCfdiSeleccionado = null;
        if (SelectorUsoCfdi is null) return;

        SelectorUsoCfdi.Elementos = ConstruirElementosUsoCfdi(_usoCfdiOpciones);

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

        var tarjetas = TarjetasOrdenadas();
        SelectorTarjeta.Elementos = ConstruirElementosTarjeta(tarjetas);
        _tarjetaSeleccionada = null;
        SelectorTarjeta.IndiceSeleccionado = -1;
        OnPropertyChanged(nameof(MostrarTarjetas));
        OnPropertyChanged(nameof(MostrarSelectorTarjeta));

        if (MostrarTarjetas)
        {
            // Por defecto "Sin tarjeta"; restaurar la tarjeta guardada si aplica.
            SelectorTarjeta.IndiceSeleccionado = 0;
            var tarjetaId = Preferences.Default.Get(PrefTarjeta, string.Empty);
            if (!string.IsNullOrEmpty(tarjetaId))
            {
                var tIdx = tarjetas.FindIndex(t => t.Id == tarjetaId);
                if (tIdx >= 0)
                {
                    _tarjetaSeleccionada = tarjetas[tIdx];
                    SelectorTarjeta.IndiceSeleccionado = tIdx + 1;
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
        var tarjetas = TarjetasOrdenadas();
        _tarjetaSeleccionada = TarjetaDesdeIndice(tarjetas, indice);
        if (_tarjetaSeleccionada is not null)
            Preferences.Default.Set(PrefTarjeta, _tarjetaSeleccionada.Id);
        else
            Preferences.Default.Remove(PrefTarjeta); // "Sin tarjeta" → no recordar tarjeta
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

        await AgregarCapturaAsync(fileName);
    }

    private Task AgregarCapturaAsync(string fileName)
    {
        var captura = new CapturaLote { TipoCaptura = TipoCaptura, FileName = fileName };
        _logs.Log($"[PaginaCaptura] AgregarCaptura — path: '{captura.Path}'");

        _capturas.Insert(0, captura);
        CapturaSeleccionada = captura;

        AppState.Instance.CapturasLote = [.. _capturas];
        _logs.Log($"[PaginaCaptura] AgregarCaptura — existe={File.Exists(captura.Path)}");

        return Task.CompletedTask;
    }

    private async Task VerImagenAsync(CapturaLote captura)
        => await Shell.Current.GoToAsync(nameof(VisorImagenPage),
               new Dictionary<string, object> { ["path"] = captura.DisplayPath });

    private async Task EliminarCapturaAsync(CapturaLote captura)
    {
        bool confirmar = await _servicioAlerta.MostrarAsync(
            "Eliminar imagen",
            "¿Estás seguro de que deseas eliminar esta imagen?",
            confirmarText: "Eliminar",
            cancelarText: "Cancelar");

        if (!confirmar) return;

        _capturas.Remove(captura);
        if (ReferenceEquals(CapturaSeleccionada, captura))
            CapturaSeleccionada = _capturas.FirstOrDefault();
        AppState.Instance.CapturasLote = _capturas.Count > 0 ? [.. _capturas] : null;

        if (!captura.EsCompartida && File.Exists(captura.Path))
        {
            try { File.Delete(captura.Path); }
            catch (Exception ex) { _logs.Log($"[PaginaCaptura] EliminarCaptura — error al borrar archivo: {ex.Message}"); }
        }
    }

    private void OnCambiarTipoCredito(object sender, TappedEventArgs e) => UsarAutoservicio = !UsarAutoservicio;

    private void OnDesglosarIepsTapped(object sender, TappedEventArgs e) => DesglosarIeps = !DesglosarIeps;
    private void OnSoloEvidenciaTapped(object sender, TappedEventArgs e) => SoloEvidencia = !SoloEvidencia;
    private void OnCapturaRemotaTapped(object sender, TappedEventArgs e) => CapturaRemota = !CapturaRemota;

    private void OnEsUrgenteTapped(object sender, TappedEventArgs e)
    {
        if (!PuedeSerUrgente) return;
        EsUrgente = !EsUrgente;
    }

    // ── Botón "Enviar" desplegable: flyout con Captura / Autoservicio ─────────

    private bool _mostrarFlyoutCredito;
    public bool MostrarFlyoutCredito
    {
        get => _mostrarFlyoutCredito;
        private set
        {
            if (_mostrarFlyoutCredito == value) return;
            _mostrarFlyoutCredito = value;
            OnPropertyChanged();
        }
    }

    // Router del botón Enviar: con ambos créditos pregunta (abre el selector); con un solo tipo envía directo.
    private async Task IniciarEnvioAsync()
    {
        if (!PuedeEnviar) return;

        if (TieneAmbosCreditos)
            await AbrirFlyoutCreditoAsync();
        else
            await EnviarAsync();
    }

    private async void OnCerrarFlyoutCredito(object sender, TappedEventArgs e) => await CerrarFlyoutCreditoAsync();

    // Al elegir el tipo en el selector (regla 3: ambos créditos) se fija el tipo y se envía.
    private async void OnSeleccionarCreditoCaptura(object sender, TappedEventArgs e)
    {
        UsarAutoservicio = false;
        await CerrarFlyoutCreditoAsync();
        await EnviarAsync();
    }

    private async void OnSeleccionarCreditoAuto(object sender, TappedEventArgs e)
    {
        UsarAutoservicio = true;
        await CerrarFlyoutCreditoAsync();
        await EnviarAsync();
    }

    // ── Límites del aviso de horario ─────────────────────────────────────────
    // La tira de la mascota es alta y va pegada al borde izquierdo, así que sus dos
    // extremos tienen que calcularse: arriba, para no taparle los formularios (cuyo alto
    // cambia al desplegar "Más opciones"); abajo, para no pelearse con la barra de
    // botones ni con el selector "Quién captura" cuando está abierto.
    // Se resuelve con el Margin y no desplazándola: desplazar una tira de este alto la
    // metería encima de los formularios, que es justo lo que se quiere evitar.
    // Los números salen del XAML de esta misma página; si allá cambian, actualizarlos aquí.

    /// <summary>Margen inferior de la tarjeta del selector (<c>FlyoutCredito</c>).</summary>
    private const double MargenInferiorFlyout = 74;

    /// <summary>Margen lateral de la tarjeta del selector cuando ocupa el ancho completo.</summary>
    private const double MargenLateralFlyout = 20;

    /// <summary>Margen inferior del aviso con la barra de botones a la vista.</summary>
    private const double MargenInferiorAviso = 56;

    /// <summary>Aire entre la tira de la mascota y la tarjeta del selector.</summary>
    private const double AireEntreAvisoYFlyout = 12;

    /// <summary>
    /// Margen izquierdo del aviso: CERO, el control arranca en el filo de la pantalla.
    /// El aire de los globos lo pone la propia vista (su Border interno lleva los 8 px);
    /// aquí no, porque la mascota necesita llegar hasta el borde y en Android lo que se
    /// sale de los límites del control se recorta.
    /// </summary>
    private const double MargenIzquierdoAviso = 0;

    private void ActualizarMargenAviso()
    {
        // ZonaCapturas es la fila de las fotos; su Y es exactamente donde terminan los
        // formularios. Antes del primer layout vale 0 y la tira arranca arriba, que es
        // el comportamiento seguro (se corrige en cuanto haya medidas).
        var arriba = Math.Max(ZonaCapturas.Y, 0);

        AvisoMascotaHorario.Margin =
            new Thickness(MargenIzquierdoAviso, arriba, 0, MargenInferiorAviso);
    }

    /// <summary>
    /// Con la tira de la mascota en pantalla, el selector se repliega a <b>todo lo que
    /// queda libre a la derecha de ella</b>: arranca donde termina el dibujo y llega
    /// hasta el borde derecho. Así conviven sin taparse y sin que ninguno de los dos
    /// tenga que encogerse en alto.
    /// </summary>
    /// <remarks>
    /// El límite lo da <see cref="MascotaVinetaView.AnchoOcupado"/> y no una mitad de
    /// pantalla: la mascota ocupa ~123 px de ancho real, así que partir por la mitad le
    /// regalaba al hueco unos 70 px que nadie usaba y dejaba al selector más estrecho de
    /// lo necesario.
    /// <para>
    /// Sin la tira el selector recupera su ancho completo: no tiene sentido dejarlo
    /// angosto cuando no hay nada que esquivar.
    /// </para>
    /// </remarks>
    private void ActualizarMargenFlyout()
    {
        var izquierda = AvisoMascotaHorario.IsVisible
            ? AvisoMascotaHorario.AnchoOcupado + AireEntreAvisoYFlyout
            : MargenLateralFlyout;

        FlyoutCredito.Margin =
            new Thickness(izquierda, 0, MargenLateralFlyout, MargenInferiorFlyout);
    }

    private async Task AbrirFlyoutCreditoAsync()
    {
        // Se anima el contenedor, no el flyout: arrastra también al aviso de horario
        // para que los dos entren como una sola pieza.
        MostrarFlyoutCredito = true;
        PanelCredito.Opacity = 0;
        PanelCredito.TranslationY = 14;
        PanelCredito.Scale = 0.92;

        // Antes de animar: si la tarjeta entrara ancha y luego se encogiera, se vería
        // saltar. La tira de la mascota no se toca, sólo cede el lado derecho.
        ActualizarMargenFlyout();

        await Task.WhenAll(
            PanelCredito.FadeTo(1, 180, Easing.CubicOut),
            PanelCredito.TranslateTo(0, 0, 220, Easing.SpringOut),
            PanelCredito.ScaleTo(1, 220, Easing.SpringOut));
    }

    private async Task CerrarFlyoutCreditoAsync()
    {
        if (!MostrarFlyoutCredito) return;

        await Task.WhenAll(
            PanelCredito.FadeTo(0, 140, Easing.CubicIn),
            PanelCredito.TranslateTo(0, 14, 140, Easing.CubicIn),
            PanelCredito.ScaleTo(0.92, 140, Easing.CubicIn));

        MostrarFlyoutCredito = false;
    }

    private async Task EnviarAsync()
    {
        if (AppState.Instance.ModoOffline)
        {
            await _servicioAlerta.MostrarAsync("Sin conexión", "Tus fotos están guardadas. Envíalas cuando recuperes internet.");
            return;
        }

        // ── Punto 1: Validar campos obligatorios ─────────────────────────────
        var cuentaFiscal = AppState.Instance.CuentaFiscalActual;
        if (cuentaFiscal is null)
        {
            await _servicioToast.MostrarAsync("Selecciona una cuenta fiscal.", ToastIcono.Warning, ToastPosicion.Bottom);
            return;
        }

        var idxFP = SelectorFormaPago.IndiceSeleccionado;
        var formaPago = idxFP >= 0 && idxFP < FormasPago.Count ? FormasPago[idxFP] : null;
        if (formaPago is null)
        {
            await _servicioToast.MostrarAsync("Selecciona el método de pago.", ToastIcono.Warning, ToastPosicion.Bottom);
            return;
        }

        // La tarjeta es opcional: si el usuario elige "Sin tarjeta" (índice 0) o no
        // tiene tarjetas registradas, se envía "0000" como terminación del medio de pago.
        var requiereTarjeta = formaPago.Codigo is "4" or "28";
        var tarjetas = TarjetasOrdenadas();
        var tarjeta = requiereTarjeta
            ? TarjetaDesdeIndice(tarjetas, SelectorTarjeta.IndiceSeleccionado)
            : null;

        var idxUso = SelectorUsoCfdi.IndiceSeleccionado;
        var usoCfdi = idxUso >= 0 && idxUso < _usoCfdiOpciones.Count ? _usoCfdiOpciones[idxUso] : null;
        if (usoCfdi is null)
        {
            await _servicioToast.MostrarAsync("Selecciona el uso de CFDI.", ToastIcono.Warning, ToastPosicion.Bottom);
            return;
        }

        // ── Punto 2: Validar créditos disponibles en AppState (del tipo activo) ─
        var creditosAppState = CreditosActivos;
        if (creditosAppState <= 0)
        {
            await _servicioToast.MostrarAsync("No tienes créditos suficientes.", ToastIcono.Error, ToastPosicion.Bottom);
            return;
        }

        List<double>? montosCierre = null;
        if (SoloEvidencia && !CapturaRemota)
        {
            if (!TryConstruirMontosCierre(out var montos, out var mensajeError))
            {
                await _servicioToast.MostrarAsync(mensajeError, ToastIcono.Warning, ToastPosicion.Bottom);
                return;
            }

            montosCierre = montos;
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
        var cantidadEnviadaLote   = 0;

        try
        {
            // ── Punto 3a: Crear el lote en el servidor ───────────────────────
            var loteRequest = new CreaLoteCaptura
            {
                CuentaFiscalId = cuentaFiscal.CuentaFiscalId,
                Tipo = SoloEvidencia ? TipoProcesoCaptura.Evidencia : TipoCaptura,
                ClaveUsoCfdi = usoCfdi.Codigo,
                ClaveFormaPago = formaPago.Codigo,
                TerminacionMedioPago = requiereTarjeta ? (tarjeta?.UltimosDigitos ?? "0000") : string.Empty,
                Comentario = NotasAdicionales,
                DesglosarIEPS = DesglosarIeps,
                CapturaRemota = false,   // Checkbox oculto — siempre se envía false
                Urgente = EsUrgente && DateTime.Now.Hour < 20,
                ProcesoAsociadoId = _procesoAsociadoId,
                EsAutoservicio = UsarAutoservicio
            };

            var loteResult = await _servicioTranscript.CrearLoteAsync(loteRequest);
            if (!loteResult.Ok)
            {
                // Sin loteId → no hay lote que completar
                if (loteResult.Error?.HttpCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    await _servicioToast.MostrarAsync("No cuentas con créditos suficientes.", ToastIcono.Error, ToastPosicion.Bottom);
                }
                else
                {
                    await _servicioToast.MostrarAsync("Ha ocurrido un error. Inténtalo de nuevo más tarde.", ToastIcono.Error, ToastPosicion.Bottom);
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
                    await _servicioToast.MostrarAsync("Ha ocurrido un error. Inténtalo de nuevo más tarde.", ToastIcono.Error, ToastPosicion.Bottom);
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
                        cantidadEnviadaLote = cantidadAEnviar;

                        // ── Punto 5a: Convertir fotos a PDF antes de subir ───
                        var capturasAEnviar = _capturas.Reverse().Take(cantidadAEnviar).ToList();
                        var rutasPdf = new List<string>();
                        bool errorProcesamiento = false;

                        for (int idx = 0; idx < capturasAEnviar.Count; idx++)
                        {
                            var captura = capturasAEnviar[idx];
                            var idxLocal = idx;
                            var progresoPdf = new Progress<double>(p =>
                            {
                                double baseProgress = 0.4 + 0.3 * ((idxLocal + p) / capturasAEnviar.Count);
                                EnviandoProgreso = baseProgress;
                                _ = AnimarProgresoAsync((float)baseProgress);
                            });

                            try
                            {
                                var rutaPdf = await _procesadorDocumento.ProcesarYGenerarPdfAsync(
                                    captura.Path,
                                    progresoPdf);
                                captura.PdfFileName = Path.GetFileName(rutaPdf);
                                rutasPdf.Add(rutaPdf);
                            }
                            catch (Exception ex)
                            {
                                _logs.Error($"[Captura] Error al generar PDF para {captura.FileName}: {ex.Message}");
                                errorProcesamiento = true;
                                break;
                            }
                        }

                        if (errorProcesamiento)
                        {
                            await _servicioToast.MostrarAsync("Ha ocurrido un error al procesar las imágenes.", ToastIcono.Error, ToastPosicion.Bottom);
                        }
                        else
                        {
                            // ── Punto 5b: Subir PDFs al Blob Storage ─────────
                            var progresoBlobCallback = new Progress<double>(p =>
                            {
                                EnviandoProgreso = 0.7 + 0.3 * p;
                                _ = AnimarProgresoAsync((float)EnviandoProgreso);
                            });

                            var subirResult = await _servicioTranscript.SubirArchivosBlobAsync(
                                precargaResult.Payload.SasToken, rutasPdf, progresoBlobCallback);

                            if (!subirResult.Ok)
                                await _servicioToast.MostrarAsync("Ha ocurrido un error al intentar enviar su captura.", ToastIcono.Error, ToastPosicion.Bottom);
                            else
                                exitoso = true;
                        }
                    }
                }
            }
        }
        catch
        {
            await _servicioToast.MostrarAsync("Ha ocurrido un error. Inténtalo de nuevo más tarde.", ToastIcono.Error, ToastPosicion.Bottom);
        }

        // ── Punto 6: Completar el lote ───────────────────────────────────────
        // Se llama sólo si el lote fue creado Y el usuario no canceló en el diálogo de créditos.
        if (loteId.HasValue && !canceladoPorUsuario)
        {
            DtoCierreLote? cierreLote = null;
            if (SoloEvidencia && !CapturaRemota)
                cierreLote = new DtoCierreLote { Montos = (montosCierre ?? []).Take(cantidadEnviadaLote).ToList() };

            var completarResult = await _servicioTranscript.CompletarLoteAsync(loteId.Value, cierreLote);
            if (completarResult.Ok && exitoso)
            {
                await Task.Delay(400); // Pausa breve para ver el 100 %

                // Eliminar imágenes CV y PDFs temporales tras envío exitoso
                foreach (var captura in _capturas)
                {
                    if (File.Exists(captura.Path))
                        try { File.Delete(captura.Path); } catch { }

                    if (!string.IsNullOrEmpty(captura.PdfPath) && File.Exists(captura.PdfPath))
                        try { File.Delete(captura.PdfPath); } catch { }
                }

                AppState.Instance.CapturasLote = null;
                _capturas.Clear();
                CapturaSeleccionada = null;
                SoloEvidencia = false;
                CapturaRemota = false;
                EsUrgente = false;
                await _servicioSesion.GetLicenciaAsync();
                await _servicioToast.MostrarAsync("¡Envío completado!", ToastIcono.Info, ToastPosicion.Bottom);
                FacturacionPage.PendienteActualizarFacturas = true;
                FacturacionPage.CapturaRecienCreadaFiltroFecha = DateTimeOffset.UtcNow;
                DashboardPage.PendienteActualizar = true;
                await Shell.Current.GoToAsync("..");
            }
            // En cualquier otro caso: terminar proceso sin eliminar capturas
        }

        EstaEnviando = false;
    }

    private bool TryConstruirMontosCierre(out List<double> montos, out string mensajeError)
    {
        montos = [];
        mensajeError = string.Empty;

        var capturasEnOrdenDeEnvio = _capturas.Reverse().ToList();

        for (var i = 0; i < capturasEnOrdenDeEnvio.Count; i++)
        {
            var captura = capturasEnOrdenDeEnvio[i];
            var textoMonto = (captura.MontoTexto ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(textoMonto))
            {
                mensajeError = $"Captura el monto del ticket #{i + 1} antes de enviar.";
                return false;
            }

            if (!decimal.TryParse(textoMonto, NumberStyles.Number, CultureInfo.CurrentCulture, out var montoDecimal) &&
                !decimal.TryParse(textoMonto, NumberStyles.Number, CultureInfo.InvariantCulture, out montoDecimal))
            {
                mensajeError = $"El monto del ticket #{i + 1} no tiene un formato válido.";
                return false;
            }

            if (montoDecimal < 0)
            {
                mensajeError = $"El monto del ticket #{i + 1} debe ser mayor o igual a cero.";
                return false;
            }

            montos.Add((double)montoDecimal);
        }

        return true;
    }

    private async Task CancelarAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnCapturasCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<CapturaLote>())
                item.PropertyChanged -= OnCapturaPropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<CapturaLote>())
                item.PropertyChanged += OnCapturaPropertyChanged;
        }

        ActualizarTitulosMontos();

        if (_capturas.Count == 0)
            CapturaSeleccionada = null;
        else if (CapturaSeleccionada is null || !_capturas.Contains(CapturaSeleccionada))
            CapturaSeleccionada = _capturas[0];

        AppState.Instance.CapturasLote = [.. _capturas];

        OnPropertyChanged(nameof(TieneCapturas));
        OnPropertyChanged(nameof(ColumnSpanCamara));
        OnPropertyChanged(nameof(PuedeEnviar));
        NotificarPanelCentral();
    }

    private void OnCapturaPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CapturaLote.MontoTexto))
            AppState.Instance.CapturasLote = [.. _capturas];
    }

    private void ActualizarTitulosMontos()
    {
        var total = _capturas.Count;
        for (var i = 0; i < total; i++)
            _capturas[i].MontoTitulo = $"Monto ticket {total - i}";
    }

    // ── Drawable: círculo de progreso ─────────────────────────────────────────

    private sealed class CircularProgressDrawable : IDrawable
    {
        public float Progress { get; set; }   // 0.0 – 1.0

        // Color del arco. Lo fija PaginaCaptura según el tipo de crédito en uso.
        public Color? ColorArco { get; set; }

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
            canvas.StrokeColor   = ColorArco ?? UIHelpers.GetColor("Primary");
            canvas.StrokeSize    = strokeWidth;
            canvas.StrokeLineCap = LineCap.Round;

            var sweep = Progress * 360f;
            canvas.DrawArc(cx - radius, cy - radius, radius * 2f, radius * 2f,
                           270f, 270f + sweep, false, false);
        }
    }

}
