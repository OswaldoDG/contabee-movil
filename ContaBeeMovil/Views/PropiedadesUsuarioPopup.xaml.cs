using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using CommunityToolkit.Maui.Views;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Views;

public partial class PropiedadesUsuarioPopup : Popup
{
    private readonly IServicioCrm _servicioCrm;
    private readonly IServicioToast _toast;
    private readonly Guid _cfid;
    private readonly Guid _usuarioId;
    private List<PropiedadUsuarioCF> _originales = [];
    private bool _valorOriginalCaptura;

    public PropiedadesUsuarioPopup(IServicioCrm servicioCrm, IServicioToast toast, Guid cfid, Guid usuarioId, string nombre, bool esPropio = false)
    {
        InitializeComponent();
        _servicioCrm = servicioCrm;
        _toast       = toast;
        _cfid        = cfid;
        _usuarioId   = usuarioId;

        LblTitulo.Text    = nombre;
        BadgeYo.IsVisible = esPropio;

        var info = DeviceDisplay.MainDisplayInfo;
        double density = info.Density > 0 ? info.Density : 1;
        CardBorder.WidthRequest = (info.Width / density) - 40;

        SwitchCaptura.Toggled += (_, _) => BtnGuardar.IsEnabled = SwitchCaptura.IsToggled != _valorOriginalCaptura;

        _ = CargarAsync();
    }

    private async Task CargarAsync()
    {
        SetCargando(true);
        var resp = await _servicioCrm.GetPropiedadesUsuario(_cfid, _usuarioId);
        if (!resp.Ok)
        {
            bool es403 = resp.Error?.HttpCode == System.Net.HttpStatusCode.Forbidden;
            await _toast.MostrarAsync(
                es403 ? "Solo el propietario puede gestionar permisos" : resp.Error?.Mensaje ?? "Error al cargar propiedades",
                ToastIcono.Error);
            await CloseAsync();
            return;
        }
        _originales = resp.Payload ?? [];
        _valorOriginalCaptura   = EsBoolTrue(Obtener("UsuarioCaptura"));
        SwitchCaptura.IsToggled = _valorOriginalCaptura;
        SetCargando(false);
    }

    private async void OnGuardar(object sender, EventArgs e)
    {
        SetCargando(true);
        try
        {
            var resp = await _servicioCrm.SetPropiedadUsuario(_cfid, _usuarioId, "UsuarioCaptura", SwitchCaptura.IsToggled ? "1" : "0");
            if (!resp.Ok)
            {
                bool es403 = resp.Error?.HttpCode == System.Net.HttpStatusCode.Forbidden;
                await _toast.MostrarAsync(
                    es403 ? "Solo el propietario puede gestionar permisos" : resp.Error?.Mensaje ?? "Error al guardar",
                    ToastIcono.Error);
            }
            else
            {
                await _toast.MostrarAsync("Propiedades guardadas", ToastIcono.Info);
                await CloseAsync();
            }
        }
        finally
        {
            SetCargando(false);
        }
    }

    private async void OnCancelar(object sender, EventArgs e) => await CloseAsync();

    private void SetCargando(bool cargando)
    {
        Spinner.IsRunning     = cargando;
        Spinner.IsVisible     = cargando;
        FormContent.IsVisible = !cargando;
        if (cargando) BtnGuardar.IsEnabled = false;
    }

    private string Obtener(string prop) =>
        _originales.FirstOrDefault(p => p.Propiedad == prop)?.ValorPropiedad ?? "";

    private static bool EsBoolTrue(string val) => val == "1";
}
