using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Views;

public partial class ResumenLicenciaPopup : CommunityToolkit.Maui.Views.Popup
{
    public ResumenLicenciaPopup()
    {
        InitializeComponent();
        CargarDatos();
    }

    private void CargarDatos()
    {
        var cuenta = AppState.Instance.CuentaFiscalActual;
        var lic    = AppState.Instance.Licenciamiento;

        LblRfc.Text = cuenta?.Rfc ?? "—";

        LblDisponibles.Text = (lic?.CreditosDisponibles ?? 0).ToString();
        //LblConsumidos.Text  = consumidos.ToString();
        //LblTotal.Text       = $"de {total} en total";
    }

    private async void OnCerrar(object sender, EventArgs e) => await CloseAsync(CancellationToken.None);
}
