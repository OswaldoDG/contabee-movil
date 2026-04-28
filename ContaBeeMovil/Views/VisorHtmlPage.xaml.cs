namespace ContaBeeMovil.Views;

public partial class VisorHtmlPage : ContentPage
{
    private const string SeñalFinal = "visor-app://fin";

    private const string ScriptDeteccionFinal = @"
        (function() {
            function alFinal() {
                return (window.innerHeight + window.scrollY) >= (document.body.scrollHeight - 8);
            }
            function notificar() {
                if (alFinal()) { window.location.href = 'visor-app://fin'; }
            }
            window.addEventListener('scroll', notificar, { passive: true });
            window.addEventListener('resize', notificar);
            setTimeout(notificar, 300);
        })();
    ";

    private readonly bool _requiereScrollAlFinal;

    public VisorHtmlPage(string titulo, string contenidoHtml, bool requiereScrollAlFinal = false)
    {
        InitializeComponent();
        _requiereScrollAlFinal = requiereScrollAlFinal;
        TituloLabel.Text = titulo;
        BtnRegresar.IsEnabled = !requiereScrollAlFinal;
        Visor.Source = new HtmlWebViewSource { Html = contenidoHtml };
    }

    public static async Task<VisorHtmlPage> DesdeArchivoAsync(string titulo, string nombreArchivo, bool requiereScrollAlFinal = false)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(nombreArchivo);
        using var reader = new StreamReader(stream);
        var html = await reader.ReadToEndAsync();
        return new VisorHtmlPage(titulo, html, requiereScrollAlFinal);
    }

    private async void OnVisorNavegado(object? sender, WebNavigatedEventArgs e)
    {
        if (_requiereScrollAlFinal && e.Result == WebNavigationResult.Success)
            await Visor.EvaluateJavaScriptAsync(ScriptDeteccionFinal);
    }

    private void OnVisorNavegando(object? sender, WebNavigatingEventArgs e)
    {
        if (_requiereScrollAlFinal && e.Url == SeñalFinal)
        {
            e.Cancel = true;
            BtnRegresar.IsEnabled = true;
        }
    }

    private async void OnRegresarClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
