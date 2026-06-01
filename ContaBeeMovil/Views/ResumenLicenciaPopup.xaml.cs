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

        var total       = lic?.CreditosCaptura ?? 0;
        var consumidos  = lic?.CreditosCapturaConsumo ?? 0;
        var disponibles = Math.Max(0, total - consumidos);

        LblDisponibles.Text = disponibles.ToString();
        //LblConsumidos.Text  = consumidos.ToString();
        //LblTotal.Text       = $"de {total} en total";
    }

    private async void OnCerrar(object sender, EventArgs e) => await CloseAsync(CancellationToken.None);
}
