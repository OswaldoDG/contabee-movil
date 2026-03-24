using Contabee.Api.abstractions;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.RecuperarPass;

public partial class RecuperarPassPage : ContentPage
{
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IToastService _toastService;

    public RecuperarPassPage(IServicioIdentidad servicioIdentidad, IToastService toastService)
    {
        InitializeComponent();
        _servicioIdentidad = servicioIdentidad;
        _toastService = toastService;
    }

    private void OnEmailTextChanged(object? sender, TextChangedEventArgs e)
    {
        BtnRestablecer.IsEnabled = !string.IsNullOrWhiteSpace(e.NewTextValue);
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        var paginaLogin = MauiProgram.Services.GetRequiredService<PaginaLogin>();
        Application.Current!.Windows[0].Page = paginaLogin;
    }

    private async void OnRestablecerClicked(object? sender, EventArgs e)
    {
        var email = EntryEmail.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email))
        {
            EmailLayout.HasError = true;
            return;
        }

        EmailLayout.HasError = false;
        LoadingIndicator.IsRunning = true;
        FormContainer.IsEnabled = false;

        try
        {
            var resultado = await _servicioIdentidad.RecuperarPassword(email);

            if (resultado.Ok)
            {
                await _toastService.ShowAsync(
                    "Se ha enviado un enlace a el email para recuperar su contraseña",
                    type: ToastType.Success,
                    position: ToastPosition.Bottom);

                var paginaLogin = MauiProgram.Services.GetRequiredService<PaginaLogin>();
                Application.Current!.Windows[0].Page = paginaLogin;
            }
            else
            {
                await _toastService.ShowAsync(
                    "No fue posible hacer la solicitud, intenta de nuevo más tarde",
                    type: ToastType.Error,
                    position: ToastPosition.Bottom);
            }
        }
        catch
        {
            await _toastService.ShowAsync(
                "No fue posible hacer la solicitud, intenta de nuevo más tarde",
                type: ToastType.Error,
                position: ToastPosition.Bottom);
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            FormContainer.IsEnabled = true;
        }
    }
}
