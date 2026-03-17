namespace ContaBeeMovil.Services.Camara;

public interface IServicioCamara
{
    // Métodos para tomar foto
    Task<string> TomarFotoAsync();
    Task<string> ProcesarImagenAsync(string imagePath);

    // Métodos para QR
    Task<string> EscanearQrAsync();
    void SetScannedQrResult(string result);
}
