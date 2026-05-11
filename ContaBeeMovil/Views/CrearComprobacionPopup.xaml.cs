using CommunityToolkit.Maui.Views;

namespace ContaBeeMovil.Views;

public partial class CrearComprobacionPopup : Popup
{
    public record ResultadoCrearComprobacion(
        Guid UsuarioReceptorId,
        decimal Monto,
        int PorcentajeComprobar,
        DateTime Cierre,
        string? Descripcion);

    private readonly Action<ResultadoCrearComprobacion?> _onResult;
    private readonly List<Guid> _destinatariosIds = [];

    public CrearComprobacionPopup(
        IReadOnlyList<(Guid UsuarioId, string Nombre)> destinatarios,
        Action<ResultadoCrearComprobacion?> onResult)
    {
        InitializeComponent();
        _onResult = onResult;

        var display = DeviceDisplay.MainDisplayInfo;
        var density = display.Density <= 0 ? 1 : display.Density;
        var screenWidth = display.Width / density;
        var screenHeight = display.Height / density;

        CardBorder.WidthRequest = Math.Min(screenWidth - 40, 420);
        CardBorder.MaximumHeightRequest = Math.Max(320, Math.Min(screenHeight - 64, 680));

        var hoy = DateTime.Today;
        LblCreacion.Text = DateTime.Now.ToString("d/M/yyyy");
        PickerCierre.Format = "dd/MM/yyyy";
        PickerCierre.MinimumDate = hoy;
        PickerCierre.Date = hoy.AddDays(1);

        var etiquetas = new List<string>();
        foreach (var destinatario in destinatarios)
        {
            etiquetas.Add(destinatario.Nombre);
            _destinatariosIds.Add(destinatario.UsuarioId);
        }

        SelectorDestinatario.Elementos = etiquetas;
        SelectorDestinatario.IndiceSeleccionado = etiquetas.Count > 0 ? 0 : -1;
    }

    private async void OnCancelar(object sender, EventArgs e)
    {
        _onResult(null);
        await CloseAsync(CancellationToken.None);
    }

    private async void OnCrear(object sender, EventArgs e)
    {
        var indiceSeleccionado = SelectorDestinatario.IndiceSeleccionado;
        if (indiceSeleccionado < 0 && _destinatariosIds.Count > 0)
            indiceSeleccionado = 0;

        if (indiceSeleccionado < 0
            || indiceSeleccionado >= _destinatariosIds.Count)
        {
            if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page1)
                await page1.DisplayAlert("Validación", "Selecciona un destinatario.", "Aceptar");
            return;
        }

        if (_destinatariosIds[indiceSeleccionado] == Guid.Empty)
        {
            if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page0)
                await page0.DisplayAlert("Validación", "Aún no se cargan los usuarios destinatarios. Intenta de nuevo en unos segundos.", "Aceptar");
            return;
        }

        if (!decimal.TryParse(EntryMonto.Text, out var monto) || monto <= 0)
        {
            if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page2)
                await page2.DisplayAlert("Validación", "Ingresa un monto válido.", "Aceptar");
            return;
        }

        if (!int.TryParse(EntryPorcentaje.Text, out var porcentaje) || porcentaje <= 0 || porcentaje > 100)
        {
            if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page3)
                await page3.DisplayAlert("Validación", "Ingresa un porcentaje entre 1 y 100.", "Aceptar");
            return;
        }

        var resultado = new ResultadoCrearComprobacion(
            _destinatariosIds[indiceSeleccionado],
            monto,
            porcentaje,
            PickerCierre.Date ?? DateTime.Today.AddDays(1),
            EntryDescripcion.Text?.Trim());

        _onResult(resultado);
        await CloseAsync(CancellationToken.None);
    }
}
