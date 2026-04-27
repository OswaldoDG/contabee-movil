using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using Contabee.Api.Ecommerce;
using ContaBeeMovil.Services;
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

    public ICommand BuscarCuponCommand { get; }
    public ICommand AplicarCuponCommand { get; }

    public PaginaCuponesViewModel(
        IServicioEcommerce servicioEcommerce,
        IServicioSesion servicioSesion,
        IServicioAlerta servicioAlerta)
    {
        _servicioEcommerce = servicioEcommerce;
        _servicioSesion = servicioSesion;
        _servicioAlerta = servicioAlerta;

        BuscarCuponCommand = new Command(async () => await RegistrarCuponPorCodigoAsync());
        AplicarCuponCommand = new Command<CuponUsuario>(async item => await AplicarCuponAsync(item));
    }

    public async Task CargarCuponesAsync()
    {
        if (EstaCargando) return;

        EstaCargando = true;
        try
        {
            _ = await _servicioSesion.LeeIdDeDispositivo();
            var cupones = await _servicioEcommerce.CuponesUsuario();

            Cupones.Clear();
            foreach (var item in cupones ?? [])
                Cupones.Add(item);
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

            if (registrado is null || !registrado.Aplicado)
            {
                await _servicioAlerta.MostrarAsync("Cupón", "No se pudo registrar el cupón.", confirmarText: "OK", verBotonCancelar: false);
                return;
            }

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
                await _servicioAlerta.MostrarAsync("Cupón", "No se pudo activar el cupón.", confirmarText: "OK", verBotonCancelar: false);
                return;
            }
        }
        finally
        {
            EstaCargando = false;
        }

        await CargarCuponesAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}