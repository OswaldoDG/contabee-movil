using Contabee.Api.abstractions;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Views;

public partial class LoteCapturaCardView : ContentView
{
    private static bool _ocupado;

    public LoteCapturaCardView() => InitializeComponent();

    private async void OnDescargarImagen(object sender, TappedEventArgs e)
        => await DescargarYCompartir("imagen", BtnCamara, SpinnerCamara);

    private async void OnDescargarPdf(object sender, TappedEventArgs e)
        => await DescargarYCompartir("pdf", BtnPdf, SpinnerPdf);

    private async void OnDescargarXml(object sender, TappedEventArgs e)
        => await DescargarYCompartir("xml", BtnXml, SpinnerXml);

    private async Task DescargarYCompartir(string tipo, Border btn, ActivityIndicator spinner)
    {
        if (_ocupado) return;
        if (BindingContext is not ContaBeeMovil.Pages.FacturacionPage.ItemConConsecutivo item)
            return;

        var servicio = IPlatformApplication.Current.Services
            .GetRequiredService<IServicioTranscript>();
        var toast = IPlatformApplication.Current.Services
            .GetRequiredService<IToastService>();

        _ocupado = true;
        SetBusy(btn, spinner, true);
        try
        {
            var archivo = await servicio.DescargarArchivoAsync(item.Datos.Id, tipo);
            if (archivo is null)
            {
                await toast.ShowAsync("No se pudo descargar el archivo.", ToastType.Error, position: ToastPosition.Bottom);
                return;
            }

            string extension = ObtenerExtension(archivo.Value.TipoContenido);
            string fileName  = $"captura_{item.Datos.Id}.{extension}";
            string filePath  = Path.Combine(FileSystem.CacheDirectory, fileName);

            await File.WriteAllBytesAsync(filePath, archivo.Value.Contenido);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Compartir captura",
                File  = new ShareFile(filePath)
            });
        }
        catch
        {
            await toast.ShowAsync("No se pudo descargar el archivo.", ToastType.Error, position: ToastPosition.Bottom);
        }
        finally
        {
            _ocupado = false;
            SetBusy(btn, spinner, false);
        }
    }

    private static void SetBusy(Border btn, ActivityIndicator spinner, bool busy)
    {
        btn.IsVisible      = !busy;
        spinner.IsVisible  = busy;
        spinner.IsRunning  = busy;
    }

    private static string ObtenerExtension(string? contentType) => contentType switch
    {
        string ct when ct.Contains("pdf")  => "pdf",
        string ct when ct.Contains("png")  => "png",
        string ct when ct.Contains("jpeg") => "jpg",
        string ct when ct.Contains("jpg")  => "jpg",
        string ct when ct.Contains("webp") => "webp",
        string ct when ct.Contains("gif")  => "gif",
        string ct when ct.Contains("bmp")  => "bmp",
        string ct when ct.Contains("xml")  => "xml",
        _                                  => "bin"
    };
}
