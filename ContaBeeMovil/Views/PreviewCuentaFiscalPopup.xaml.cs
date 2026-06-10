using CommunityToolkit.Maui.Views;
using Contabee.Api.Crm;
using ContaBeeMovil.Helpers;

namespace ContaBeeMovil.Views;

public partial class PreviewCuentaFiscalPopup : Popup
{
    public bool Confirmado { get; private set; }
    public TipoPersonaFiscal TipoSeleccionado { get; private set; } = TipoPersonaFiscal.Fisica;
    public bool Compartido { get; private set; }

    public PreviewCuentaFiscalPopup(CuentaFiscal cuenta)
    {
        InitializeComponent();

        var screenWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        ContenedorPopup.MinimumWidthRequest = screenWidth * 0.70;
        ContenedorPopup.WidthRequest = Math.Min(screenWidth * 0.88, 500);

        PoblarCampos(cuenta);
        ConfigurarSelectores(cuenta);
    }

    private void PoblarCampos(CuentaFiscal cuenta)
    {
        LblRfc.Text = cuenta.Rfc ?? "—";

        var nombre = string.Join(" ", new[] { cuenta.Nombre, cuenta.Apellido1, cuenta.Apellido2 }
            .Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        LblNombre.Text = string.IsNullOrWhiteSpace(nombre) ? "—" : nombre;
        LblNombre.IsVisible = !string.IsNullOrWhiteSpace(nombre);


        var (codigo, descripcion) = ObtenerRegimen(cuenta.ClaveRegimenFiscal, cuenta.Regimen);
        LblRegimenCodigo.Text = codigo;
        LblRegimenDesc.Text = descripcion;
        LblRegimenDesc.IsVisible = !string.IsNullOrWhiteSpace(descripcion);

        var direcciones = cuenta.Direcciones?.Where(d => !string.IsNullOrWhiteSpace(FormatearDireccion(d))).ToList()
                          ?? new List<DireccionFiscal>();

        if (direcciones.Count > 0)
        {
            SeccionDireccion.IsVisible = true;

            // Con más de una dirección se limita la altura y el ScrollView activa el scroll.
            ScrollDirecciones.MaximumHeightRequest = direcciones.Count > 1 ? 130 : double.PositiveInfinity;

            for (int i = 0; i < direcciones.Count; i++)
            {
                if (i > 0)
                    ContenedorDirecciones.Add(new BoxView { Style = (Style)Application.Current!.Resources["Divider"] });

                if (direcciones.Count > 1)
                {
                    var header = new Label { Text = $"Dirección {i + 1}", FontSize = 10, FontAttributes = FontAttributes.Bold };
                    header.SetDynamicResource(Label.TextColorProperty, "SecondaryText");
                    ContenedorDirecciones.Add(header);
                }

                var lbl = new Label { Text = FormatearDireccion(direcciones[i]), FontSize = 12 };
                lbl.SetDynamicResource(Label.TextColorProperty, "PrimaryText");
                ContenedorDirecciones.Add(lbl);
            }
        }
        else
        {
            SeccionDireccion.IsVisible = false;
        }
    }

    private void ConfigurarSelectores(CuentaFiscal cuenta)
    {
        SelectorTipo.Elementos = new List<string> { "Física", "Moral" };
        SelectorTipo.IndiceSeleccionado = cuenta.Tipo == TipoPersonaFiscal.Moral ? 1 : 0;
        Compartido = true;
    }

    private static (string codigo, string descripcion) ObtenerRegimen(string? clave, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(clave))
            return ("—", fallback ?? "");

        var descripcion = RegimenFiscalProvider.GetRegimenFiscal(PersonaType.Fisica)
            .Concat(RegimenFiscalProvider.GetRegimenFiscal(PersonaType.Moral))
            .FirstOrDefault(r => r.Codigo == clave)?.Descripcion ?? "";

        return (clave, descripcion);
    }

    private static string FormatearDireccion(DireccionFiscal d)
    {
        var vialidad = string.Join(" ", new[] { d.TipoVialidad, d.NombreVialidad }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

        var numeros = string.IsNullOrWhiteSpace(d.NumExterior) ? null : $"No. {d.NumExterior}";
        if (!string.IsNullOrWhiteSpace(d.NumInterior))
            numeros = (numeros is null ? "" : numeros + " ") + $"Int. {d.NumInterior}";

        var partes = new[]
        {
            string.Join(" ", new[] { vialidad, numeros }.Where(s => !string.IsNullOrWhiteSpace(s))),
            d.Colonia,
            d.Municipio,
            d.EntidadFederativa,
            string.IsNullOrWhiteSpace(d.CodigoPostal) ? null : $"C.P. {d.CodigoPostal}"
        };

        return string.Join(", ", partes.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private void OnCancelar(object sender, EventArgs e)
    {
        Confirmado = false;
        _ = CloseAsync();
    }

    private void OnConfirmar(object sender, EventArgs e)
    {
        Confirmado = true;
        TipoSeleccionado = SelectorTipo.IndiceSeleccionado == 1 ? TipoPersonaFiscal.Moral : TipoPersonaFiscal.Fisica;
        _ = CloseAsync();
    }
}
