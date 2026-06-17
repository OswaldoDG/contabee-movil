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

        LblCreditosCaptura.Text = (lic?.CreditosDisponibles      ?? 0).ToString();
        LblCreditosColab.Text   = (lic?.CreditosColabDisponibles ?? 0).ToString();
        LblCreditosAuto.Text    = (lic?.CreditosAutoDisponibles  ?? 0).ToString();
    }

    private async void OnCerrar(object sender, EventArgs e) => await CloseAsync(CancellationToken.None);
}
