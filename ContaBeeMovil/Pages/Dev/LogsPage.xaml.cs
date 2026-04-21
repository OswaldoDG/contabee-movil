using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Pages.Dev;

public partial class LogsPage : ContentPage
{
    private readonly IServicioLogs _servicioLogs;

    public LogsPage(IServicioLogs servicioLogs)
    {
        InitializeComponent();
        _servicioLogs = servicioLogs;
        BindingContext = AppState.Instance;
    }

    private void OnLimpiarClicked(object? sender, EventArgs e)
    {
        _servicioLogs.Limpiar();
    }

    private async void OnLogTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid { BindingContext: string texto })
            await Clipboard.Default.SetTextAsync(texto);
    }

    private async void OnCopiarLogClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string texto })
            await Clipboard.Default.SetTextAsync(texto);
    }
}
