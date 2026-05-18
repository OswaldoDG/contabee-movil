using Contabee.Api.Transcript;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ContaBeeMovil.Models;

public class CapturaLote : INotifyPropertyChanged
{
    public TipoProcesoCaptura TipoCaptura { get; set; }

    private string _montoTexto = string.Empty;
    public string MontoTexto
    {
        get => _montoTexto;
        set
        {
            if (_montoTexto == value) return;
            _montoTexto = value;
            OnPropertyChanged();
        }
    }

    private string _montoTitulo = "Monto ticket";
    public string MontoTitulo
    {
        get => _montoTitulo;
        set
        {
            if (_montoTitulo == value) return;
            _montoTitulo = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Solo el nombre del archivo (sin directorio). Reconstruir el path completo
    /// con FileSystem.AppDataDirectory para evitar paths absolutos obsoletos.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Path completo calculado en tiempo de ejecución desde FileName.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public string Path => string.IsNullOrEmpty(FileName)
        ? string.Empty
        : System.IO.Path.Combine(FileSystem.AppDataDirectory, FileName);

    /// <summary>
    /// Indica que la imagen fue recibida desde otra app (Share Extension).
    /// </summary>
    public bool EsCompartida { get; set; } = false;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
