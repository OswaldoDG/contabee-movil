using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using Contabee.Api.abstractions;
using Contabee.Pages.Registro;
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

        // Suscribir cambios del password para actualizar iconos dinámicamente
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(RegistroViewModel.Password))
                UpdatePasswordRuleIcons(_viewModel.Password); // ✅ sin cast
        };
    }

    void OnTogglePasswordClicked(object? sender, EventArgs e)
    {
        if (PasswordEntry is null || TogglePasswordButton is null)
            return;

        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;

        TogglePasswordButton.Icon(PasswordEntry.IsPassword ? MaterialIcons.VisibilityOff : MaterialIcons.Visibility);
       
    }

    void OnToggleConfirmPasswordClicked(object? sender, EventArgs e)
    {
        if (ConfirmPasswordEntry is null || ToggleConfirmPasswordButton is null)
            return;

        ConfirmPasswordEntry.IsPassword = !ConfirmPasswordEntry.IsPassword;

        ToggleConfirmPasswordButton.Icon(ConfirmPasswordEntry.IsPassword ? MaterialIcons.VisibilityOff : MaterialIcons.Visibility);
        
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