using CommunityToolkit.Mvvm.ComponentModel;

namespace ContaBeeMovil.PageModels.Camara;

public partial class QRPageModel : ObservableObject
{
    [ObservableProperty]
    private string _tipoPersona = "Física";

    [ObservableProperty]
    private bool _compartido;

    public List<string> TiposPersona => new() { "Física", "Moral" };
}
