using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ContaBeeMovil.Pages.Tienda;

public class CategoriaTabModel : INotifyPropertyChanged
{
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public List<ProductoIAPModel> Productos { get; set; } = [];

    private bool _esSeleccionada;
    public bool EsSeleccionada
    {
        get => _esSeleccionada;
        set
        {
            if (_esSeleccionada == value) return;
            _esSeleccionada = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
