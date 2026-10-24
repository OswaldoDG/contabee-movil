using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using Contabee.Api.Ecommerce;
using Contabee.Api.Transcript;
using ContaBee.Pages.Cupones;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Pages.Perfil;
using ContaBeeMovil.Models;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Pages.Dashboard;

public class DashboardViewModel : INotifyPropertyChanged
{
    private readonly IServicioTranscript _servicioTranscript;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioEcommerce _servicioEcommerce;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioLogs _logs;

    // Resumen cacheado por cuenta fiscal + mes: se pinta al instante y se refresca en segundo
    // plano (los pods de Azure arrancan en frío y la respuesta puede tardar). Dentro de esta
    // ventana no se vuelve a pedir, para no repetir la consulta en cada cambio de pestaña.
    private static readonly TimeSpan _vigenciaResumen = TimeSpan.FromMinutes(2);
    private sealed record ResumenGuardado(ResumenCapturaCuentaFiscal Resumen, bool SinActividad);

    private Task? _cargaEnCurso;
    private string? _claveEnCurso;

    private int _mes;
    private int _anio;
    private double _totalFacturado;
    private int _creditosRestantes;
    private int _emitidas;
    private int _declinadas;
    private int _pendientes;
    private bool _estaCargando;
    private bool _tieneError;
    private bool _sinActividad;
    private bool _estaRefrescando;
    private bool _tieneCuponBienvenida;

    // El aviso de cupón no se vuelve a pedir en cada cambio a la pestaña Inicio: los cambios
    // reales llegan por CuponesVersion / CuentaFiscalActual (forzados) o por pull-to-refresh.
    private DateTime _ultimaConsultaCupones = DateTime.MinValue;
    private static readonly TimeSpan _vigenciaCupones = TimeSpan.FromMinutes(30);
    private string _mensajeError = string.Empty;
    private ObservableCollection<DiaActividadItem> _datosGrafica = [];

    public DashboardViewModel(IServicioTranscript servicioTranscript, IServicioAlerta servicioAlerta, IServicioEcommerce servicioEcommerce, IServicioSesion servicioSesion, IServicioLogs logs)
    {
        _servicioTranscript = servicioTranscript;
        _servicioAlerta = servicioAlerta;
        _servicioEcommerce = servicioEcommerce;
        _servicioSesion = servicioSesion;
        _logs = logs;
        _mes = DateTime.Now.Month;
        _anio = DateTime.Now.Year;

        MesAnteriorCommand  = new Command(async () => await NavegaMesAsync(-1));
        MesSiguienteCommand = new Command(
            async () => await NavegaMesAsync(1),
            () => new DateTime(_anio, _mes, 1) < new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1));
        RefreshCommand      = new Command(async () => await ReiniciarAlMesActualAsync());
        PullRefreshCommand  = new Command(async () => await PullRefreshAsync());
        VerCuponesCommand   = new Command(async () => await Shell.Current.GoToAsync(nameof(PaginaCupones)));

        AppState.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.CuentaFiscalActual))
            {
                _ = CargarEstadisticasAsync(forzarActualizacion: true);
                _ = CargarCuponBienvenidaAsync(forzar: true);
            }
            else if (e.PropertyName is nameof(AppState.CuponesVersion))
            {
                _ = CargarCuponBienvenidaAsync(forzar: true);
            }
            else if (e.PropertyName is nameof(AppState.Licenciamiento))
            {
                OnPropertyChanged(nameof(CreditosCapturaDisponibles));
                OnPropertyChanged(nameof(CreditosColabDisponibles));
                OnPropertyChanged(nameof(CreditosAutoDisponibles));
            }
        };
    }

    #region Properties

    public string MesAnioTexto =>
        new DateTime(_anio, _mes, 1).ToString("MMMM yyyy",
            new System.Globalization.CultureInfo("es-MX"));

    public string MesNombreTexto =>
        new DateTime(_anio, _mes, 1).ToString("MMMM",
            new System.Globalization.CultureInfo("es-MX"));

    public int UltimoDiaMes =>
        DateTime.DaysInMonth(_anio, _mes);

    public double MaximoEjeX =>
        (double)DateTime.DaysInMonth(_anio, _mes);

    public double MaximoEjeY =>
        _datosGrafica.Count > 0
            ? Math.Max(5, (double)_datosGrafica.Max(d => Math.Max(d.Emitidas, d.Solicitadas)))
            : 5;

    public double TotalFacturado
    {
        get => _totalFacturado;
        set { _totalFacturado = value; OnPropertyChanged(); }
    }

    public int CreditosRestantes
    {
        get => _creditosRestantes;
        set { _creditosRestantes = value; OnPropertyChanged(); }
    }

    public int CreditosCapturaDisponibles  => AppState.Instance.Licenciamiento?.CreditosDisponibles     ?? 0;
    public int CreditosColabDisponibles    => AppState.Instance.Licenciamiento?.CreditosColabDisponibles ?? 0;
    public int CreditosAutoDisponibles     => AppState.Instance.Licenciamiento?.CreditosAutoDisponibles  ?? 0;

    public int Emitidas
    {
        get => _emitidas;
        set { _emitidas = value; OnPropertyChanged(); }
    }

    public int Declinadas
    {
        get => _declinadas;
        set { _declinadas = value; OnPropertyChanged(); }
    }

    public int Pendientes
    {
        get => _pendientes;
        set { _pendientes = value; OnPropertyChanged(); }
    }

    public bool EstaCargando
    {
        get => _estaCargando;
        set
        {
            _estaCargando = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotLoading));
            OnPropertyChanged(nameof(MostrarContenido));
            OnPropertyChanged(nameof(MostrarError));
        }
    }

    public bool IsNotLoading => !_estaCargando;

    public bool TieneError
    {
        get => _tieneError;
        set
        {
            _tieneError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MostrarContenido));
            OnPropertyChanged(nameof(MostrarError));
        }
    }

    public string MensajeError
    {
        get => _mensajeError;
        set { _mensajeError = value; OnPropertyChanged(); }
    }

    public bool MostrarContenido => !_estaCargando && !_tieneError;
    public bool MostrarError     => !_estaCargando && _tieneError;

    public ObservableCollection<DiaActividadItem> DatosGrafica
    {
        get => _datosGrafica;
        set { _datosGrafica = value; OnPropertyChanged(); }
    }

    public bool SinActividad
    {
        get => _sinActividad;
        set { _sinActividad = value; OnPropertyChanged(); }
    }

    public bool EstaRefrescando
    {
        get => _estaRefrescando;
        set { _estaRefrescando = value; OnPropertyChanged(); }
    }

    public bool TieneCuponBienvenida
    {
        get => _tieneCuponBienvenida;
        private set { _tieneCuponBienvenida = value; OnPropertyChanged(); }
    }

    #endregion

    #region Commands

    public ICommand MesAnteriorCommand { get; }
    public ICommand MesSiguienteCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand PullRefreshCommand { get; }
    public ICommand VerCuponesCommand { get; }

    #endregion

    #region Public Methods

    public async Task LoadDataAsync(bool forzarActualizacion = false)
    {
        await Task.WhenAll(
            CargarEstadisticasAsync(forzarActualizacion),
            CargarCuponBienvenidaAsync(forzarActualizacion));
    }

    #endregion

    #region Private Methods

    private async Task CargarCuponBienvenidaAsync(bool forzar = false)
    {
        if (!forzar && DateTime.UtcNow - _ultimaConsultaCupones < _vigenciaCupones)
            return;

        try
        {
            var cupones = await _servicioEcommerce.CuponesUsuario();
            TieneCuponBienvenida = cupones.Any(c => c.Tipo == TipoCupon.CreditoCaptura && (c.Aplicado == false || c.Fecha == null));
            _ultimaConsultaCupones = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logs.Error($"[Dashboard] Error cupones: {ex.GetType().Name} - {ex.Message}");
        }
    }

    private async Task PullRefreshAsync()
    {
        EstaRefrescando = true;
        var recarga = Task.WhenAll(
            CargarEstadisticasAsync(forzarActualizacion: true),
            CargarCuponBienvenidaAsync(forzar: true),
            _servicioSesion.GetLicenciaAsync());

        // El indicador solo confirma el gesto: los datos en pantalla siguen siendo válidos y lo
        // nuevo se pinta solo al llegar. Si el API está despertando (~15 s), dejarlo girando
        // hace parecer que algo anda mal.
        await Task.WhenAny(recarga, Task.Delay(TimeSpan.FromMilliseconds(800)));
        EstaRefrescando = false;

        try
        {
            await recarga;
        }
        catch (Exception ex)
        {
            _logs.Error($"[Dashboard] Pull-to-refresh: {ex.GetType().Name} - {ex.Message}");
        }
    }

    private async Task ReiniciarAlMesActualAsync()
    {
        _mes = DateTime.Now.Month;
        _anio = DateTime.Now.Year;
        OnPropertyChanged(nameof(MesAnioTexto));
        OnPropertyChanged(nameof(MesNombreTexto));
        OnPropertyChanged(nameof(UltimoDiaMes));
        OnPropertyChanged(nameof(MaximoEjeX));
        ((Command)MesSiguienteCommand).ChangeCanExecute();
        await CargarEstadisticasAsync(forzarActualizacion: true);
    }

    private async Task NavegaMesAsync(int delta)
    {
        var fecha = new DateTime(_anio, _mes, 1).AddMonths(delta);
        _mes = fecha.Month;
        _anio = fecha.Year;
        OnPropertyChanged(nameof(MesAnioTexto));
        OnPropertyChanged(nameof(MesNombreTexto));
        OnPropertyChanged(nameof(UltimoDiaMes));
        OnPropertyChanged(nameof(MaximoEjeX));
        ((Command)MesSiguienteCommand).ChangeCanExecute();
        await CargarEstadisticasAsync();
    }

    private async Task CargarEstadisticasAsync(bool forzarActualizacion = false)
    {
        var cfid = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
        if (cfid == null || cfid == Guid.Empty)
        {
            var cuentas = AppState.Instance.CuentasFiscales;
            if (cuentas is { Count: > 0 })
            {
                // Autoseleccionar la primera cuenta disponible;
                // el PropertyChanged disparará CargarEstadisticasAsync de nuevo.
                AppState.Instance.CuentaFiscalActual = cuentas[0];
                return;
            }

            // null = error de red/sesión que ya fue manejado por AuthHandler o el CoordinadorSesion
            if (cuentas is null)
                return;

            // Lista vacía: el usuario no tiene cuentas fiscales. No redirigir —
            // el login ya maneja el primer acceso; aquí el usuario puede navegar libremente.
            return;
        }

        var clave = ClaveCache(cfid.Value, _anio, _mes);

        // Al cambiar de cuenta llegan dos cargas seguidas (PropertyChanged + reactivación de la
        // pestaña): la segunda espera a la que ya va en camino en vez de repetir la consulta.
        if (_claveEnCurso == clave && _cargaEnCurso is { IsCompleted: false })
        {
            await _cargaEnCurso;
            return;
        }

        _claveEnCurso = clave;
        _cargaEnCurso = CargarResumenAsync(cfid.Value, _anio, _mes, clave, forzarActualizacion);
        await _cargaEnCurso;
    }

    private async Task CargarResumenAsync(Guid cfid, int anio, int mes, string clave, bool forzar)
    {
        // El usuario pudo cambiar de mes o de cuenta mientras esperábamos al servidor:
        // en ese caso la respuesta solo se guarda en caché, no se pinta.
        bool esVigente() => clave == ClaveActual();

        var cache = CacheConsultas.Leer<ResumenGuardado>(clave);
        if (cache != null)
        {
            TieneError   = false;
            SinActividad = cache.Datos.SinActividad;
            AplicarDatos(cache.Datos.Resumen);

            if (!forzar && DateTime.UtcNow - cache.GuardadoUtc < _vigenciaResumen)
            {
                _logs.Info($"[Dashboard] Caché reciente para {mes}/{anio}, sin consultar");
                return;
            }
        }

        _logs.Info($"[Dashboard] Cargando {mes}/{anio} forzar={forzar} cache={cache != null}");

        // Con caché en pantalla el refresco es silencioso; el spinner solo sale la primera vez.
        EstaCargando = cache == null;
        try
        {
            var resultado = await _servicioTranscript.GetEstadisticas(cfid, anio, mes);
            if (!resultado.Ok || resultado.Payload == null)
            {
                var errorMsg = resultado.Error?.Mensaje ?? string.Empty;

                // Asociación desactivada/eliminada (403): el CoordinadorSesion ya reconcilia
                // la sesión (cambia de cuenta / modo limitado) y el Dashboard se recarga solo.
                // NO mostramos el error de "verifica tu conexión" (sería engañoso).
                if (CodigosErrorApi.EsAsociacionInactiva(resultado.Error?.HttpCode, errorMsg))
                {
                    if (esVigente())
                    {
                        TieneError   = false;
                        SinActividad = false;
                    }
                    return;
                }

                if (errorMsg.Contains("CRM-LIC-INEXISTENTE", StringComparison.OrdinalIgnoreCase))
                {
                    _logs.Warn($"[Dashboard] Sin actividad (CRM-LIC-INEXISTENTE) para {mes}/{anio}");
                    var datosVacios = new ResumenCapturaCuentaFiscal { Ano = anio, Mes = mes };
                    CacheConsultas.Guardar(clave, new ResumenGuardado(datosVacios, SinActividad: true));
                    if (esVigente())
                    {
                        SinActividad = true;
                        TieneError   = false;
                        AplicarDatos(datosVacios);
                    }
                }
                else
                {
                    _logs.Warn($"[Dashboard] Error API: {resultado.Error?.Codigo} - {resultado.Error?.Mensaje} (caché={cache != null})");
                    if (cache == null && esVigente())
                    {
                        TieneError   = true;
                        MensajeError = "No se pudieron cargar los datos.\nVerifica tu conexión e intenta de nuevo.";
                    }
                }
                return;
            }

            var p = resultado.Payload;
            CacheConsultas.Guardar(clave, new ResumenGuardado(p, SinActividad: false));
            _logs.Info($"[Dashboard] OK — Ano={p.Ano} Mes={p.Mes} Solicitudes={p.Solicitudes} Emitidas={p.Emitidas} Pendientes={p.Pendientes} Declinadas={p.Declinadas} CreditosAdquiridos={p.CreditosAdquiridos} CreditosRestantes={p.CreditosRestantes} TotalFacturado={p.TotalFacturado}");

            if (!esVigente()) return;
            SinActividad = false;
            TieneError   = false;
            AplicarDatos(p);
        }
        catch (Exception ex)
        {
            _logs.Error($"[Dashboard] Excepción: {ex.GetType().Name} - {ex.Message} (caché={cache != null})");
            if (cache == null && esVigente())
            {
                TieneError   = true;
                MensajeError = "Ocurrió un error inesperado.\nIntenta de nuevo.";
            }
        }
        finally
        {
            // Si el usuario ya está en otro mes/cuenta, esa carga maneja su propio spinner.
            if (esVigente())
                EstaCargando = false;
        }
    }

    private void AplicarDatos(ResumenCapturaCuentaFiscal r)
    {
        TotalFacturado    = r.TotalFacturado;
        CreditosRestantes = r.CreditosRestantes ?? 0;
        Emitidas          = r.Emitidas;
        Declinadas        = r.Declinadas;
        Pendientes        = r.Pendientes;
        DatosGrafica      = BuildChartData(r.EmitidasDia, r.SolicitadasDia);
        OnPropertyChanged(nameof(MaximoEjeY));
    }

    // ── Caché ────────────────────────────────────────────────────────────────

    private static string ClaveCache(Guid cfid, int anio, int mes)
        => CacheConsultas.Clave(CacheConsultas.Dashboard, cfid, $"{anio}-{mes}");

    private string? ClaveActual()
    {
        var cfid = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
        return cfid is Guid id ? ClaveCache(id, _anio, _mes) : null;
    }

    // ── Chart ─────────────────────────────────────────────────────────────────

    private static ObservableCollection<DiaActividadItem> BuildChartData(
        ICollection<int>? emitidasDia,
        ICollection<int>? solicitadasDia)
    {
        var emitidas    = emitidasDia?.ToList() ?? [];
        var solicitadas = solicitadasDia?.ToList() ?? [];
        int dias        = Math.Max(emitidas.Count, solicitadas.Count);

        var items = new ObservableCollection<DiaActividadItem>();
        for (int i = 0; i < dias; i++)
        {
            items.Add(new DiaActividadItem
            {
                Dia         = i + 1,
                Emitidas    = i < emitidas.Count    ? emitidas[i]    : 0,
                Solicitadas = i < solicitadas.Count ? solicitadas[i] : 0,
            });
        }
        return items;
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
