using Contabee.Api.abstractions;

namespace ContaBeeMovil.Pages.Perfil;

[QueryProperty(nameof(CuentaFiscalIdStr), "cfid")]
[QueryProperty(nameof(RfcParam), "rfc")]
public partial class ResumenLicenciaRFCPage : ContentPage
{
    private readonly IServicioCrm _servicioCrm;

    private Guid _cfid;
    private string _rfc = "—";
    private bool _cargado;

    public string CuentaFiscalIdStr
    {
        set => _cfid = Guid.TryParse(value, out var g) ? g : Guid.Empty;
    }

    public string RfcParam
    {
        set => _rfc = Uri.UnescapeDataString(value ?? "—");
    }

    public ResumenLicenciaRFCPage(IServicioCrm servicioCrm)
    {
        InitializeComponent();
        _servicioCrm = servicioCrm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_cargado) return;
        _cargado = true;

        LblRfc.Text = _rfc;
        await CargarLicenciamientoAsync();
    }

    private async Task CargarLicenciamientoAsync()
    {
        SetLoading(true);

        var res = await _servicioCrm.GetLicenciamiento(_cfid);

        if (res.Ok && res.Payload != null)
        {
            var lic = res.Payload;
            LblDisponibles.Text = lic.CreditosDisponibles.ToString();
            // LblConsumidos.Text = consumidos.ToString();
            // LblTotal.Text = $"de {total} en total";
        }
        else
        {
            LblDisponibles.Text = "0";
        }

        SetLoading(false);
    }

    private void SetLoading(bool cargando)
    {
        LoadingOverlay.IsVisible = cargando;
    }
}
