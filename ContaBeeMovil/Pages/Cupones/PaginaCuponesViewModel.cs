using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ContaBee.Pages.Cupones;

public class PaginaCuponesViewModel : INotifyPropertyChanged
{
    private readonly IServicioCrm _servicioCrm;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioAlerta _servicioAlerta;

    private bool _estaCargando;
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

    public ObservableCollection<LicenciamientoGratuito> Cupones { get; } = [];

    public ICommand AplicarCuponCommand { get; }

    public PaginaCuponesViewModel(
        IServicioCrm servicioCrm,
        IServicioSesion servicioSesion,
        IServicioAlerta servicioAlerta)
    {
        _servicioCrm = servicioCrm;
        _servicioSesion = servicioSesion;
        _servicioAlerta = servicioAlerta;

        AplicarCuponCommand = new Command<LicenciamientoGratuito>(async item => await AplicarCuponAsync(item));
    }

    public async Task CargarCuponesAsync()
    {
        if (EstaCargando) return;

        var cuenta = AppState.Instance.CuentaFiscalActual;
        if (cuenta is null || string.IsNullOrWhiteSpace(cuenta.Rfc)) return;

        EstaCargando = true;
        try
        {
            var dispositivoId = await _servicioSesion.LeeIdDeDispositivo();
            var respuesta = await _servicioCrm.GetLicenciamientosFactibles(cuenta.Rfc, dispositivoId, cuenta.CuentaFiscalId);

            if (!respuesta.Ok)
            {
                await _servicioAlerta.MostrarAsync("Cupones", "No se pudieron cargar los cupones.", confirmarText: "OK", verBotonCancelar: false);
                return;
            }

            Cupones.Clear();
            foreach (var item in respuesta.Payload ?? [])
                Cupones.Add(item);
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private async Task AplicarCuponAsync(LicenciamientoGratuito? item)
    {
        if (item is null || EstaCargando) return;

        var cuenta = AppState.Instance.CuentaFiscalActual;
        if (cuenta is null || string.IsNullOrWhiteSpace(cuenta.Rfc)) return;

        EstaCargando = true;
        try
        {
            var dispositivoId = await _servicioSesion.LeeIdDeDispositivo();

            var solicitud = await _servicioCrm.SolicitarLicenciamientoDemo(
                cuenta.Rfc, dispositivoId, cuenta.CuentaFiscalId, item.Cupon);

            if (!solicitud.Ok || string.IsNullOrWhiteSpace(solicitud.Payload?.Token))
            {
                await _servicioAlerta.MostrarAsync("Cupón", "No se pudo solicitar la aplicación del cupón.", confirmarText: "OK", verBotonCancelar: false);
                return;
            }

            var activacion = await _servicioCrm.ActivarLicenciamientoDemo(
                solicitud.Payload.Token, dispositivoId, cuenta.CuentaFiscalId);

            if (!activacion.Ok)
            {
                await _servicioAlerta.MostrarAsync("Cupón", "No se pudo activar el cupón.", confirmarText: "OK", verBotonCancelar: false);
                return;
            }

            Cupones.Remove(item);
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