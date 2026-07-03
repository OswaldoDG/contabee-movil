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
    private string _fileName = string.Empty;
    public string FileName
    {
        get => _fileName;
        set
        {
            if (_fileName == value) return;
            _fileName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Path));
            OnPropertyChanged(nameof(DisplayPath));
        }
    }

    /// <summary>
    /// Path completo calculado en tiempo de ejecución desde FileName.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public string Path => string.IsNullOrEmpty(FileName)
        ? string.Empty
        : System.IO.Path.Combine(FileSystem.AppDataDirectory, FileName);

    /// <summary>
    /// Nombre del PDF generado localmente tras el procesamiento (sin directorio).
    /// Null hasta que ServicioProcesadorDocumento procese la foto.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public string? PdfFileName { get; set; }

    /// <summary>
    /// Path completo del PDF generado, o null si aún no se procesó.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public string? PdfPath => string.IsNullOrEmpty(PdfFileName)
        ? null
        : System.IO.Path.Combine(FileSystem.AppDataDirectory, PdfFileName);

    /// <summary>
    /// Lo que muestra el carousel: la foto tal cual se tomó.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public string DisplayPath => Path;

    /// <summary>
    /// Indica que la imagen fue recibida desde otra app (Share Extension).
    /// </summary>
    public bool EsCompartida { get; set; } = false;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
