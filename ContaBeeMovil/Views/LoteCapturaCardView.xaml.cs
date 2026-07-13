using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Contabee.Api.abstractions;
using ContaBeeMovil.Pages.VisorPdf;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Views;

public partial class LoteCapturaCardView : ContentView
{
    private static bool _ocupado;

    // Scrim que atenúa toda la pantalla detrás del overlay de carga; no se puede
    // descartar tocando fuera (misma convención que el resto de popups del proyecto).
    private static readonly PopupOptions _overlayOpts = new()
    {
        PageOverlayColor = Color.FromArgb("#66000000"),
        CanBeDismissedByTappingOutsideOfPopup = false
    };

    public LoteCapturaCardView() => InitializeComponent();

    private async void OnDescargarPdf(object sender, TappedEventArgs e)
        => await DescargarYCompartir("pdf", BtnPdf, SpinnerPdf);

    private async void OnDescargarXml(object sender, TappedEventArgs e)
        => await DescargarYCompartir("xml", BtnXml, SpinnerXml);

    private async void OnDescargarImagen(object sender, TappedEventArgs e)
        => await DescargarYCompartir("imagen", BtnCamaraLeft, SpinnerCamaraLeft);

    private async Task DescargarYCompartir(string tipo, Border btn, ActivityIndicator spinner)
    {
        if (_ocupado) return;
        if (BindingContext is not ContaBeeMovil.Pages.FacturacionPage.ItemConConsecutivo item)
            return;

        var servicio = IPlatformApplication.Current.Services
            .GetRequiredService<IServicioTranscript>();
        var toast = IPlatformApplication.Current.Services
            .GetRequiredService<IServicioToast>();

        _ocupado = true;
        SetBusy(btn, spinner, true);

        // Overlay de carga a pantalla completa mientras se descarga: da una señal
        // clara de que algo está pasando (el mini spinner del botón se conserva).
        var overlay = new CargandoPopup();
        var pagina = Shell.Current as Page ?? Application.Current?.Windows[0].Page;
        bool overlayVisible = false;
        if (pagina is not null)
        {
            _ = pagina.ShowPopupAsync(overlay, _overlayOpts, CancellationToken.None);
            overlayVisible = true;
        }

        try
        {
            var archivo = await servicio.DescargarArchivoAsync(item.Datos.Id, tipo);
            if (archivo is null)
            {
                await toast.MostrarAsync("No se pudo descargar el archivo.", ToastIcono.Error, ToastPosicion.Bottom);
                return;
            }

            // El content-type del API no es confiable (backends sin soporte PDF
            // regresan octet-stream): los magic bytes del contenido son la verdad.
            string extension = EsPdf(archivo.Value.Contenido)
                ? "pdf"
                : ObtenerExtension(archivo.Value.TipoContenido);
            string fileName  = $"captura_{item.Datos.Id}.{extension}";
            string filePath  = Path.Combine(FileSystem.CacheDirectory, fileName);

            await File.WriteAllBytesAsync(filePath, archivo.Value.Contenido);

            // Cerrar el overlay antes de navegar/compartir: no debe quedar detrás
            // del visor ni de la hoja de compartir del sistema.
            if (overlayVisible)
            {
                await overlay.CloseAsync();
                overlayVisible = false;
            }

            // El ticket capturado puede ser un PDF: se abre en el visor propio
            // (zoom, rotar, descargar y compartir) en lugar de solo compartirse.
            if (tipo == "imagen" && extension == "pdf")
            {
                await Shell.Current.GoToAsync(nameof(PaginaVisorPdfPropio), new Dictionary<string, object>
                {
                    ["path"]   = filePath,
                    ["titulo"] = fileName
                });
                return;
            }

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Compartir captura",
                File  = new ShareFile(filePath)
            });
        }
        catch
        {
            await toast.MostrarAsync("No se pudo descargar el archivo.", ToastIcono.Error, ToastPosicion.Bottom);
        }
        finally
        {
            if (overlayVisible)
            {
                try { await overlay.CloseAsync(); } catch { /* cierre best-effort */ }
            }
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

    // Encabezado "%PDF"
    private static bool EsPdf(byte[]? contenido) =>
        contenido is { Length: >= 4 } &&
        contenido[0] == 0x25 && contenido[1] == 0x50 &&
        contenido[2] == 0x44 && contenido[3] == 0x46;

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
