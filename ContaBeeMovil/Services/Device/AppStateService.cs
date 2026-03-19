using CommunityToolkit.Mvvm.ComponentModel;
using Contabee.Api.crm;
using Contabee.Api.Crm;
using Contabee.Api.Identidad;
using ContaBeeMovil.Models;
using Newtonsoft.Json;

namespace ContaBeeMovil.Services.Device;

/// <summary>
/// Servicio singleton que emula el AppState de FlutterFlow.
/// Cada variable registrada aquí está disponible en toda la app,
/// detecta cambios (INotifyPropertyChanged) y persiste entre sesiones.
/// </summary>
public partial class AppState : ObservableObject
{
    // ── Singleton ──────────────────────────────────────────────────────────────
    private static readonly AppState _instance = new();
    public static AppState Instance => _instance;

    private AppState()
    {
        CargarDesdePreferencias();
    }

    // ── Helpers de persistencia ────────────────────────────────────────────────

    private static void GuardarObjeto<T>(string clave, T? valor)
    {
        var json = valor is not null ? JsonConvert.SerializeObject(valor) : string.Empty;
        Preferences.Default.Set(clave, json);
    }

    private static T? LeerObjeto<T>(string clave)
    {
        var json = Preferences.Default.Get(clave, string.Empty);
        return string.IsNullOrEmpty(json) ? default : JsonConvert.DeserializeObject<T>(json);
    }

    private void CargarDesdePreferencias()
    {
        _perfil                  = LeerObjeto<PerfilUsuario>(PrefsKeys.Perfil);
        _cuentasFiscales         = LeerObjeto<List<CuentaUsuarioResponse>>(PrefsKeys.CuentasFiscales);
        _cuentaFiscalActual      = LeerObjeto<CuentaUsuarioResponse>(PrefsKeys.CuentaFiscalActual);
        _direccionFiscalActual   = LeerObjeto<Contabee.Api.crm.DireccionFiscal>(PrefsKeys.DireccionFiscalActual);
        _mostrarNombreFiscal     = Preferences.Get(PrefsKeys.VerUserName, false);
        _recordarme              = Preferences.Get(PrefsKeys.Recordarme, false);
    }

    // ── Claves de Preferences ──────────────────────────────────────────────────
    private static class PrefsKeys
    {
        public const string Perfil                 = "AppState_Perfil";
        public const string CuentasFiscales        = "AppState_CuentasFiscales";
        public const string CuentaFiscalActual     = "AppState_CuentaFiscalActual";
        public const string DireccionFiscalActual  = "AppState_DireccionFiscalActual";
        public const string VerUserName            = "AppState_VerUserName";
        public const string Recordarme             = "AppState_Recordarme";
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  VARIABLES DE ESTADO
    //  Para agregar una nueva variable:
    //    1. Declarar el campo privado con su valor inicial.
    //    2. Exponer la propiedad pública con get/set que llame a SetProperty
    //       y persista el nuevo valor en Preferences.
    //    3. Agregar la clave en PrefsKeys.
    //    4. Cargar el valor en CargarDesdePreferencias().
    // ══════════════════════════════════════════════════════════════════════════

    // ── Perfil ─────────────────────────────────────────────────────────────────
    private PerfilUsuario? _perfil;

    /// <summary>
    /// Perfil del usuario autenticado. Notifica cambios y persiste automáticamente.
    /// </summary>
    public PerfilUsuario? Perfil
    {
        get => _perfil;
        set
        {
            if (SetProperty(ref _perfil, value))
                GuardarObjeto(PrefsKeys.Perfil, value);
        }
    }

    // ── Recordarme ────────────────────────────────────────────────────────────
    private bool _recordarme;

    /// <summary>
    /// Refleja el estado del checkbox "Recordarme" del login. Persiste entre sesiones.
    /// </summary>
    public bool Recordarme
    {
        get => _recordarme;
        set
        {
            if (SetProperty(ref _recordarme, value))
                Preferences.Set(PrefsKeys.Recordarme, value);
        }
    }

    // ── MostrarNombreFiscal (no persistida, sólo en sesión) ───────────────────
    private bool _mostrarNombreFiscal =false;

    /// <summary>
    /// Cuando es true el selector y la barra muestran el Nombre en lugar del RFC.
    /// </summary>
    public bool MostrarNombreFiscal
    {
        get => _mostrarNombreFiscal;
        set
        { 
            if (SetProperty(ref _mostrarNombreFiscal, value))
                Preferences.Set(PrefsKeys.VerUserName,value);
        }
    }

    // ── CuentaFiscalActual ─────────────────────────────────────────────────────
    private CuentaUsuarioResponse? _cuentaFiscalActual;

    /// <summary>
    /// Cuenta fiscal actualmente seleccionada. Notifica cambios y persiste automáticamente.
    /// </summary>
    public CuentaUsuarioResponse? CuentaFiscalActual
    {
        get => _cuentaFiscalActual;
        set
        {
            if (SetProperty(ref _cuentaFiscalActual, value))
            {
                GuardarObjeto(PrefsKeys.CuentaFiscalActual, value);
                DireccionFiscalActual = value?.DireccionesFiscales?.FirstOrDefault();
            }
        }
    }

    // ── DireccionFiscalActual ──────────────────────────────────────────────────
    private Contabee.Api.crm.DireccionFiscal? _direccionFiscalActual;

    /// <summary>
    /// Dirección fiscal actualmente seleccionada. Notifica cambios y persiste automáticamente.
    /// </summary>
    public Contabee.Api.crm.DireccionFiscal? DireccionFiscalActual
    {
        get => _direccionFiscalActual;
        set
        {
            if (SetProperty(ref _direccionFiscalActual, value))
                GuardarObjeto(PrefsKeys.DireccionFiscalActual, value);
        }
    }

    // ── CuentasFiscales ────────────────────────────────────────────────────────
    private List<CuentaUsuarioResponse>? _cuentasFiscales;

    /// <summary>
    /// Lista de cuentas fiscales asociadas al usuario. Notifica cambios y persiste automáticamente.
    /// </summary>
    public List<CuentaUsuarioResponse>? CuentasFiscales
    {
        get => _cuentasFiscales;
        set
        {
            if (SetProperty(ref _cuentasFiscales, value))
                GuardarObjeto(PrefsKeys.CuentasFiscales, value);
        }
    }

    // ── Tarjetas ───────────────────────────────────────────────────────────────
    private List<TarjetaModel>? _tarjetas;

    /// <summary>
    /// Tarjetas del usuario cargadas desde SecureStorage. No persiste en Preferences.
    /// </summary>
    public List<TarjetaModel>? Tarjetas
    {
        get => _tarjetas;
        set => SetProperty(ref _tarjetas, value);
    }
}
