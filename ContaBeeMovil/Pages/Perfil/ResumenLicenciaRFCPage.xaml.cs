using Contabee.Api.abstractions;
using ContaBeeMovil.Services.Dev;

namespace ContaBeeMovil.Pages.Perfil;

[QueryProperty(nameof(CuentaFiscalIdStr), "cfid")]
[QueryProperty(nameof(RfcParam), "rfc")]
public partial class ResumenLicenciaRFCPage : ContentPage
{
    private readonly IServicioCrm _servicioCrm;
    private readonly IServicioLogs _logs;

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

    public ResumenLicenciaRFCPage(IServicioCrm servicioCrm, IServicioLogs logs)
    {
        InitializeComponent();
        _servicioCrm = servicioCrm;
        _logs = logs;
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
            _logs.Log($"[ResumenLicenciaRFC] OK → cfid={_cfid} Captura={lic.CreditosDisponibles} Colab={lic.CreditosColabDisponibles} Auto={lic.CreditosAutoDisponibles}");
            LblCreditosCaptura.Text = lic.CreditosDisponibles.ToString();
            LblCreditosColab.Text   = lic.CreditosColabDisponibles.ToString();
            LblCreditosAuto.Text    = lic.CreditosAutoDisponibles.ToString();
        }
        else
        {
            _logs.Warn($"[ResumenLicenciaRFC] ERROR → cfid={_cfid} HttpCode={res.HttpCode} Mensaje={res.Error?.Mensaje}");
            LblCreditosCaptura.Text = "0";
            LblCreditosColab.Text   = "0";
            LblCreditosAuto.Text    = "0";
        }

        SetLoading(false);
    }

    private void SetLoading(bool cargando)
    {
        LoadingOverlay.IsVisible = cargando;
    }
}
