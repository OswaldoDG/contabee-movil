namespace ContaBeeMovil.Services.Camara;

public interface IServicioCamara
{
    Task<string> TomarFotoAsync();
    Task<string> ProcesarImagenAsync(string imagePath);
    Task<string> EscanearQrAsync();
    void SetScannedQrResult(string result);
}
