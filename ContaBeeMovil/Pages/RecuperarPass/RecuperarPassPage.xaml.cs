using Contabee.Api.abstractions;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.RecuperarPass;

public partial class RecuperarPassPage : ContentPage
{
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioToast _servicioToast;

    public RecuperarPassPage(IServicioIdentidad servicioIdentidad, IServicioToast servicioToast)
    {
        InitializeComponent();
        _servicioIdentidad = servicioIdentidad;
        _servicioToast = servicioToast;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        EntryEmail.Focused += OnEntryFocused;
        EntryEmail.Unfocused += OnEntryUnfocused;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        EntryEmail.Focused -= OnEntryFocused;
        EntryEmail.Unfocused -= OnEntryUnfocused;
    }

    private void OnEntryFocused(object? sender, FocusEventArgs e) =>
        LogoImage.IsVisible = false;

    private void OnEntryUnfocused(object? sender, FocusEventArgs e) =>
        LogoImage.IsVisible = true;

    private void OnEmailTextChanged(object? sender, TextChangedEventArgs e)
    {
        BtnRestablecer.IsEnabled = !string.IsNullOrWhiteSpace(e.NewTextValue);
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
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
                await _servicioToast.MostrarAsync(
                    "Se ha enviado un enlace a el email para recuperar su contraseña",
                    ToastIcono.Info, ToastPosicion.Bottom);

                await Navigation.PopAsync();
            }
            else
            {
                await _servicioToast.MostrarAsync(
                    "No fue posible hacer la solicitud, intenta de nuevo más tarde",
                    ToastIcono.Error, ToastPosicion.Bottom);
            }
        }
        catch
        {
            await _servicioToast.MostrarAsync(
                "No fue posible hacer la solicitud, intenta de nuevo más tarde",
                ToastIcono.Error, ToastPosicion.Bottom);
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            FormContainer.IsEnabled = true;
        }
    }
}
