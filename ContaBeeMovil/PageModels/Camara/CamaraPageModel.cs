using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContaBeeMovil.Services.Camara;

namespace ContaBeeMovil.PageModels.Camara;

public partial class CamaraPageModel : ObservableObject
{
    private readonly IServicioCamara _servicioCamara;

    public CamaraPageModel(IServicioCamara servicioCamara)
    {
        _servicioCamara = servicioCamara;
    }

    // Opciones opcionales que pueden rellenarse antes de abrir la cámara
    public string? TipoPersona { get; set; }
    public bool Compartido { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhoto))]
    private string _photoPath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TomarFotoCommand))]
    private bool _isBusy;

    public bool HasPhoto => !string.IsNullOrEmpty(PhotoPath);

    [RelayCommand(CanExecute = nameof(CanTomarFoto))]
    private async Task TomarFotoAsync()
    {
        IsBusy = true;
        var path = await _servicioCamara.TomarFotoAsync();
        if (!string.IsNullOrEmpty(path))
            PhotoPath = path;
        IsBusy = false;
    }

    private bool CanTomarFoto() => !IsBusy;
}
