using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContaBeeMovil.Services.Camara;

namespace ContaBeeMovil.PageModels.Camara;

public partial class TomarFotoPageModel : ObservableObject
{
    private readonly IServicioCamara _servicioCamara;

    public TomarFotoPageModel(IServicioCamara servicioCamara)
    {
        _servicioCamara = servicioCamara;
    }

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
