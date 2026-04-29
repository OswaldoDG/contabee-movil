using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using Contabee.Api.abstractions;
using Contabee.Pages.Registro;
using ContaBeeMovil.Views;
using MauiIcons.Core;
using MauiIcons.Material;
using System.Text.RegularExpressions;
using ContaBeeMovil.Helpers;
namespace ContaBeeMovil.Pages.Registro;

public partial class PaginaRegistro : ContentPage
{
    private RegistroViewModel _viewModel;

    public PaginaRegistro(RegistroViewModel viewModel)
    {
        InitializeComponent();

        // Inicializar iconos FontAwesome en los ImageButton (usa las extensiones del paquete)
        // Si tu versión del paquete expone métodos de extensión `.Icon(...)`, se usan aquí.
        // Si el compilador señala que .Icon(...) no existe, coméntalos y dime el error para ajustar.
        // Actualizar checks iniciales según ViewModel
        _viewModel = viewModel;
        BindingContext = _viewModel;
        UpdatePasswordRuleIcons(_viewModel.Password);
        UpdateButtonColor(_viewModel.PuedeRegistrar);

        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(RegistroViewModel.Password))
                UpdatePasswordRuleIcons(_viewModel.Password);
            if (args.PropertyName == nameof(RegistroViewModel.PuedeRegistrar) ||
                args.PropertyName == nameof(RegistroViewModel.Password) ||
                args.PropertyName == nameof(RegistroViewModel.ConfirmarPassword) ||
                args.PropertyName == nameof(RegistroViewModel.Nombre) ||
                args.PropertyName == nameof(RegistroViewModel.Email) ||
                args.PropertyName == nameof(RegistroViewModel.AceptaPrivacidad))
                UpdateButtonColor(_viewModel.PuedeRegistrar);
        };
    }

    void OnConfirmarPasswordTextChanged(object? sender, TextChangedEventArgs e)
    {
        var confirmar = e.NewTextValue ?? string.Empty;
        var password = _viewModel.Password ?? string.Empty;
        ErrorCoincidenciaLabel.IsVisible = !string.IsNullOrEmpty(confirmar) && confirmar != password;
    }

    void OnToggleConfirmPasswordClicked(object? sender, EventArgs e)
    {
        if (ConfirmPasswordEntry is null || ToggleConfirmPasswordButton is null)
            return;

        bool isPassword = !ConfirmPasswordEntry.IsPassword;
        ConfirmPasswordEntry.IsPassword = isPassword;
        PasswordEntry.IsPassword = isPassword;

        ToggleConfirmPasswordButton.Icon(isPassword ? MaterialIcons.VisibilityOff : MaterialIcons.Visibility);
    }

    async void OnAvisoPrivacidadTapped(object? sender, TappedEventArgs e)
    {
        var visor = await VisorHtmlPage.DesdeArchivoAsync("Aviso de privacidad", "privacidad.html");
        await Navigation.PushModalAsync(visor);
    }

    async void OnTerminosTapped(object? sender, TappedEventArgs e)
    {
        var visor = await VisorHtmlPage.DesdeArchivoAsync("Términos del servicio", "tos.html");
        await Navigation.PushModalAsync(visor);
    }

    protected override bool OnBackButtonPressed()
    {
        Navigation.PopAsync();
        return true;
    }

    void UpdateButtonColor(bool puedeRegistrar)
    {
        var primary = UIHelpers.GetColor("Primary");
        var disabled = UIHelpers.GetColor("Disabled");
        BtnCrearCuenta.BackgroundColor = puedeRegistrar ? primary : disabled;
    }

    void UpdatePasswordRuleIcons(string pwd)
    {
        if (BindingContext is RegistroViewModel vm)
        {
            var success = UIHelpers.GetColor("Primary");
            var disabled = UIHelpers.GetColor("Disabled");
            if (string.IsNullOrEmpty(pwd)) return;
            vm.EsMinimo6 = pwd.Length > 6;
            vm.TieneMayuscula=pwd.Any(char.IsUpper);
            vm.TieneNumero = pwd.Any(char.IsDigit);
            vm.TieneCaracterEspecial = Regex.IsMatch(pwd, @"[@$%&._#]");

            IconMin6.IconColor = vm.EsMinimo6? success : disabled;
            IconMayus.IconColor = vm.TieneMayuscula ? success : disabled;
            IconNumero.IconColor = vm.TieneNumero ? success : disabled;
            IconEspecial.IconColor = vm.TieneCaracterEspecial ? success : disabled;
        }
    }
}