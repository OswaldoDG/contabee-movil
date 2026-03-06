using Contabee.Api.abstractions;
using ContaBeeMovil.Services;

namespace ContaBeeMovil.Pages.Login;

public partial class PaginaLogin : ContentPage
{
    public PaginaLogin()
    {
        InitializeComponent();

        var servicioIdentidad = MauiProgram.Services.GetRequiredService<IServicioIdentidad>();
        var servicioSesion = MauiProgram.Services.GetRequiredService<IServicioSesion>();
        var notificacion = MauiProgram.Services.GetRequiredService<IServicioNotificacion>();
        BindingContext = new LoginViewModel(servicioIdentidad, servicioSesion, notificacion);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Reset form animation state when page appears
        FormContainer.Opacity = 1;
        FormContainer.TranslationX = 0;
        LogoImage.Opacity = 1;
        LogoImage.Scale = 1;
    }
}
