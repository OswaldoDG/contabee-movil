using System.Windows.Input;
using Contabee.Api;
using Contabee.Api.abstractions;
using Contabee.Api.crm;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Pages.Perfil;

public partial class RFCsPage : ContentPage
{
    private readonly IServicioCrm _servicioCrm;
    private bool _autoNavegado;

    public RFCsPage(IServicioCrm servicioCrm)
    {
        InitializeComponent();
        _servicioCrm = servicioCrm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Esperar a que termine la animación de navegación antes de poblar la lista.
        // Sin esto, Android deja un artifact gris encima del CollectionView.
        await Task.Delay(250);

        CargarCuentas();

        var cuentas = AppState.Instance.CuentasFiscales;
        if (!_autoNavegado && (cuentas == null || cuentas.Count == 0))
        {
            _autoNavegado = true;
            await AbrirRegistrar();
        }
    }

    private void CargarCuentas()
    {
        var cuentas = AppState.Instance.CuentasFiscales ?? new List<CuentaUsuarioResponse>();
        ListaCuentas.ItemsSource = cuentas.Select(c => new CuentaItem
        {
            Rfc = c.Rfc ?? "—",
            Regimen = ObtenerDescripcionRegimen(c.ClaveRegimenFiscal),
            DeleteCommand = new Command(async () => await ConfirmarEliminar(c))
        }).ToList();
    }

    private static string ObtenerDescripcionRegimen(string? clave)
    {
        if (string.IsNullOrEmpty(clave)) return "Sin régimen";
        var regimen = RegimenFiscalProvider.GetRegimenFiscal(null)
            .FirstOrDefault(r => r.Codigo == clave);
        return regimen?.Descripcion ?? clave;
    }

    private async void BtnAgregar_Clicked(object? sender, EventArgs e)
    {
        try { await AbrirRegistrar(); }
        catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
    }

    private async Task AbrirRegistrar()
    {
        await Shell.Current.GoToAsync(nameof(RegistrarRFCsPage));
    }

    private async Task ConfirmarEliminar(CuentaUsuarioResponse cuenta)
    {
        bool confirmar = await DisplayAlert(
            "Eliminar",
            "Eliminar la cuenta fiscal es un proceso irreversible ¿Desea continuar?",
            "Si", "Cancelar");

        if (!confirmar) return;

        SetLoading(true);

        Respuesta respuesta;
        if (cuenta.TipoCuenta?.Equals("Primaria", StringComparison.OrdinalIgnoreCase) == true)
            respuesta = await _servicioCrm.EliminarCuentaFiscal(cuenta.CuentaFiscalId);
        else
            respuesta = await _servicioCrm.EliminarAsociacionFiscal(cuenta.Id);

        if (respuesta.Ok)
        {
            var actualizadas = await _servicioCrm.GetAsociacionesFiscales();
            if (actualizadas.Ok && actualizadas.Payload != null)
                AppState.Instance.CuentasFiscales = actualizadas.Payload;
            SetLoading(false);
            CargarCuentas();
        }
        else
        {
            SetLoading(false);
            var mensaje = respuesta.Error?.Mensaje ?? "Error al eliminar la cuenta fiscal.";
            await DisplayAlert("Error", mensaje, "OK");
        }
    }

    private void SetLoading(bool isLoading)
    {
        LoadingOverlay.IsVisible = isLoading;
        BtnAgregar.IsEnabled = !isLoading;
    }

    private class CuentaItem
    {
        public string Rfc { get; init; } = "";
        public string Regimen { get; init; } = "";
        public ICommand DeleteCommand { get; init; } = null!;
    }
}
