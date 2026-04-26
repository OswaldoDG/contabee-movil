using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Dev;

public partial class LogsPage : ContentPage
{
    private readonly IServicioLogs _servicioLogs;
    private readonly IServicioToast _servicioToast;

    public LogsPage(IServicioLogs servicioLogs, IServicioToast servicioToast)
    {
        InitializeComponent();
        _servicioLogs = servicioLogs;
        _servicioToast = servicioToast;
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
        if (sender is not Button { CommandParameter: string texto }) return;
        await Clipboard.Default.SetTextAsync(texto);
        try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { }
        await _servicioToast.MostrarAsync("Copiado al portapapeles");
    }
}
