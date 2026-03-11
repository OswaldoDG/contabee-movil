using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace ContaBeeMovil.Services.Device;

using CommunityToolkit.Mvvm.ComponentModel;
using Contabee.Api.Crm;
using Contabee.Api.Identidad;
using ContaBeeMovil.Services.Almacenamiento;

public partial class AppState : ObservableObject
{
    private readonly IServicioAlmacenamiento _servicioAlmacenamiento;
    [ObservableProperty]
    private PerfilUsuario _perfil;
    [ObservableProperty]
    private List<AsociacionCuentaFiscalCompleta> _CuentasFiscales;


    // Constructor: carga valores persistidos
    public AppState(IServicioAlmacenamiento servicioAlmacenamiento)
    {
        _perfil = servicioAlmacenamiento.LeerObjetoPreferencia<PerfilUsuario>("perfil");
        _CuentasFiscales = servicioAlmacenamiento.LeerObjetoPreferencia<List<AsociacionCuentaFiscalCompleta>>("CuentasFiscales");
        _servicioAlmacenamiento = servicioAlmacenamiento;
    }

    // Sobrescribes OnPropertyChanged para persistir automáticamente
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(Perfil):
                _servicioAlmacenamiento.GuardarObjetoPreferencia<PerfilUsuario>("Perfil", Perfil);
                break;
            case nameof(CuentasFiscales):
                _servicioAlmacenamiento.GuardarObjetoPreferencia<List<AsociacionCuentaFiscalCompleta>>("CuentasFiscales", CuentasFiscales);
                break;
        }
    }
}
