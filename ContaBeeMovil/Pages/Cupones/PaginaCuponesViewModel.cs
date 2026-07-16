using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using Contabee.Api.Ecommerce;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ContaBee.Pages.Cupones;

public class PaginaCuponesViewModel : INotifyPropertyChanged
{
    private readonly IServicioEcommerce _servicioEcommerce;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioLogs _logs;

    private bool _estaCargando;
    private string _codigoCupon = string.Empty;

    public bool EstaCargando
    {
        get => _estaCargando;
        set
        {
            if (_estaCargando == value) return;
            _estaCargando = value;
            OnPropertyChanged();
        }
    }

    public string CodigoCupon
    {
        get => _codigoCupon;
        set
        {
            if (_codigoCupon == value) return;
            _codigoCupon = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<CuponUsuario> Cupones { get; } = [];

    public string TextoCuentaFiscalAplicacion
    {
        get
        {
            var cuenta = AppState.Instance.CuentaFiscalActual;
            if (cuenta is null) return "?";
            return string.IsNullOrWhiteSpace(cuenta.Rfc) ? "?" : cuenta.Rfc;
        }
    }

    public ICommand BuscarCuponCommand { get; }
    public ICommand AplicarCuponCommand { get; }

    public PaginaCuponesViewModel(
        IServicioEcommerce servicioEcommerce,
        IServicioSesion servicioSesion,
        IServicioAlerta servicioAlerta,
        IServicioLogs logs)
    {
        _servicioEcommerce = servicioEcommerce;
        _servicioSesion = servicioSesion;
        _servicioAlerta = servicioAlerta;
        _logs = logs;

        AppState.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.CuentaFiscalActual)
                or nameof(AppState.DireccionFiscalActual)
                or nameof(AppState.MostrarNombreFiscal))
            {
                OnPropertyChanged(nameof(TextoCuentaFiscalAplicacion));
            }

            if (e.PropertyName is nameof(AppState.CuentaFiscalActual))
            {
                _ = CargarCuponesAsync();
            }
        };

        BuscarCuponCommand = new Command(async () => await RegistrarCuponPorCodigoAsync());
        AplicarCuponCommand = new Command<CuponUsuario>(async item => await AplicarCuponAsync(item));
    }

    public async Task CargarCuponesAsync()
    {
        if (EstaCargando) return;

        _logs.Info("[Cupones] Cargando cupones");
        EstaCargando = true;
        try
        {
            _ = await _servicioSesion.LeeIdDeDispositivo();
            var cfid = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
            var cupones = await _servicioEcommerce.CuponesUsuario(cfid);

            Cupones.Clear();
            foreach (var item in cupones ?? [])
                Cupones.Add(item);

            _logs.Info($"[Cupones] Cargados — count={Cupones.Count}");
        }
        catch (Exception ex)
        {
            _logs.Error($"[Cupones] Error al cargar: {ex.GetType().Name} - {ex.Message}");
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private async Task RegistrarCuponPorCodigoAsync()
    {
        if (EstaCargando) return;

        var codigo = CodigoCupon?.Trim();
        if (string.IsNullOrWhiteSpace(codigo))
        {
            await _servicioAlerta.MostrarAsync("Cupón", "Captura un código.", confirmarText: "OK", verBotonCancelar: false);
            return;
        }

        EstaCargando = true;
        try
        {            
            var registrado = await _servicioEcommerce.AplicarCupon(codigo, new ActivacionCuponDto
            {
                Codigo = codigo,
                Activar = false
            });

            if (registrado.Codigo is null || registrado.Aplicado)
            {
                _logs.Warn("[Cupones] Cupón no válido o ya aplicado");
                await _servicioAlerta.MostrarAsync("Cupón", "Cupón no válido", confirmarText: "OK", verBotonCancelar: false);
                return;
            }

            _logs.Info("[Cupones] Cupón registrado por código");
            CodigoCupon = string.Empty;
        }
        finally
        {
            EstaCargando = false;
        }

        await CargarCuponesAsync();
    }

    private async Task AplicarCuponAsync(CuponUsuario? item)
    {
        if (item is null || EstaCargando) return;

        var cuenta = AppState.Instance.CuentaFiscalActual;
        if (cuenta is null) return;

        EstaCargando = true;
        try
        {
            _ = await _servicioSesion.LeeIdDeDispositivo();

            var payload = new ActivacionCuponDto
            {
                Codigo = item.Codigo,
                UsuarioId = cuenta.UsuarioId.ToString(),
                Activar = true
            };

            if (item.TipoCuenta == TipoCuentaCupon.CuentaFiscal)
            {
                payload.CuentaFiscalId = cuenta.CuentaFiscalId.ToString();
            }

            var activacion = await _servicioEcommerce.AplicarCupon(item.Codigo, payload);

            if (activacion is null || !activacion.Aplicado)
            {
                _logs.Warn("[Cupones] No se pudo activar el cupón");
                await _servicioAlerta.MostrarAsync("Cupón", "No se pudo activar el cupón.", confirmarText: "OK", verBotonCancelar: false);
                return;
            }

            _logs.Info($"[Cupones] Cupón aplicado — tipo={item.TipoCuenta}");
            AppState.Instance.CuponesVersion++;
        }
        finally
        {
            EstaCargando = false;
        }

        await Task.WhenAll(
            CargarCuponesAsync(),
            _servicioSesion.GetLicenciaAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}