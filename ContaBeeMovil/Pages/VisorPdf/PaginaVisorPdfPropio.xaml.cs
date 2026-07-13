using CommunityToolkit.Maui.Storage;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Notifications;
using ContaBeeMovil.Services.Pdf;

namespace ContaBeeMovil.Pages.VisorPdf;

/// <summary>
/// Visor de PDF propio: las páginas se renderizan a imágenes con las APIs del
/// sistema (<see cref="IServicioRenderPdf"/>) y se muestran en un lienzo con
/// zoom (pinch, botones y doble tap), paneo, restaurar, rotación ±90°,
/// descarga y compartir. El zoom y el paneo se aplican como transforms
/// (Scale/Translation) sobre el contenedor — sin ScrollView, para que el
/// pinch reciba el gesto completo. En plataformas sin implementación
/// (Windows, solo dev) abre el PDF con el visor del sistema.
/// </summary>
[QueryProperty(nameof(RutaArchivo), "path")]
[QueryProperty(nameof(Titulo), "titulo")]
public partial class PaginaVisorPdfPropio : ContentPage
{
    private const double ZoomMin = 1.0, ZoomMax = 4.0, ZoomPaso = 1.25;
    // Píxeles de render ≈ 2.5× el ancho de pantalla: nítido hasta ~2.5x de zoom.
    private const double Multiplicador = 2.5;

    private readonly IServicioToast _toast =
        MauiProgram.Services.GetRequiredService<IServicioToast>();
    private readonly IServicioLogs _logs =
        MauiProgram.Services.GetRequiredService<IServicioLogs>();
    private readonly IServicioRenderPdf _render =
        MauiProgram.Services.GetRequiredService<IServicioRenderPdf>();

    private string? _rutaArchivo;
    private string _nombreArchivo = "documento.pdf";
    private double _ultimoAnchoRender = -1;      // ancho (DIPs) con el que se renderizó
    private CancellationTokenSource? _debounceCts;
    private bool _ocupado;       // compartir / descargar
    private bool _renderizando;  // render en curso (rotar, carga)
    private int _grados;         // rotación del usuario: 0/90/180/270
    private double _anchoBaseDips;
    private int _anchoPxObjetivo;
    private IReadOnlyList<PaginaPdfRender> _paginas = [];
    private readonly CancellationTokenSource _cts = new();

    // Estado de los transforms (zoom alrededor del centro + paneo con clamp)
    private double _zoom = 1.0;
    private double _tx, _ty;
    private double _panInicioX, _panInicioY;
    private double _zoomInicioPinch = 1.0;
    private double _pinchAcumulado = 1.0;
    private bool _pinchActivo;
    private double _altoContenidoBase; // suma de alturas base + spacing + padding

    public PaginaVisorPdfPropio()
    {
        InitializeComponent();
        // El dimensionamiento se maneja desde el ancho REAL del lienzo (DIPs), no
        // desde DeviceDisplay: en iOS MainDisplayInfo.Width viene en puntos y salía
        // a 1/escala. SizeChanged + debounce garantiza usar el ancho ya estabilizado
        // (la primera medición puede ser transitoria durante el push de Shell).
        Lienzo.SizeChanged += OnLienzoSizeChanged;
    }

    public string RutaArchivo
    {
        set => _rutaArchivo = value;
    }

    public string Titulo
    {
        // Solo se usa como nombre al guardar/compartir. No se asigna a Title: el
        // nombre de las capturas es un número (p. ej. "captura_12345.pdf") y se
        // vería raro en la barra superior; se deja vacía.
        set => _nombreArchivo = value;
    }

    private void OnLienzoSizeChanged(object? sender, EventArgs e)
    {
        double ancho = Lienzo.Width;
        if (ancho <= 0) return;
        // Sin cambio real de ancho: nada que rehacer (evita re-render en cada layout).
        if (Math.Abs(ancho - _ultimoAnchoRender) < 1) return;
        ProgramarRender(ancho);
    }

    // Debounce: durante el push de Shell el ancho puede llegar en varias mediciones;
    // se espera a que se estabilice y se renderiza con el ancho final (no el transitorio).
    private void ProgramarRender(double anchoDips)
    {
        _debounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _debounceCts = cts;
        _ = RenderConAnchoAsync(anchoDips, cts.Token);
    }

    private async Task RenderConAnchoAsync(double anchoDips, CancellationToken token)
    {
        try { await Task.Delay(120, token); }
        catch (TaskCanceledException) { return; }
        if (token.IsCancellationRequested) return;

        // Un render en curso: reintenta cuando termine (no se pierden cambios de tamaño).
        if (_renderizando)
        {
            ProgramarRender(anchoDips);
            return;
        }

        _ultimoAnchoRender = anchoDips;
        _anchoBaseDips = anchoDips;
        double densidad = DeviceDisplay.MainDisplayInfo.Density;
        if (densidad <= 0) densidad = 1;
        // ancho_px ≈ píxeles físicos × Multiplicador (nitidez), igual que en Android.
        _anchoPxObjetivo = (int)(anchoDips * densidad * Multiplicador);
        _logs.Info($"[VisorPdfPropio] Render — ancho={anchoDips:0}dips densidad={densidad} pxObjetivo={_anchoPxObjetivo}");

        await CargarAsync();
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        // Cancela renders en curso al salir de la página. No se usa
        // OnDisappearing porque también se dispara al backgroundear la app.
        _debounceCts?.Cancel();
        _cts.Cancel();
    }

    // ── Carga y render ───────────────────────────────────────────────────────

    private async Task CargarAsync()
    {
        if (string.IsNullOrEmpty(_rutaArchivo) || !File.Exists(_rutaArchivo))
        {
            await _toast.MostrarAsync("No se encontró el documento.", ToastIcono.Error, ToastPosicion.Bottom);
            await Shell.Current.GoToAsync("..");
            return;
        }

#if ANDROID || IOS
        // El dimensionamiento (_anchoBaseDips / _anchoPxObjetivo) ya se calculó en
        // RenderConAnchoAsync a partir del ancho real (estabilizado) del lienzo.
        await RenderizarYMostrarAsync();
#else
        // Sin render propio en esta plataforma: se abre con el visor del sistema.
        OverlayCarga.IsVisible = false;
        await Launcher.Default.OpenAsync(new OpenFileRequest("Abrir PDF", new ReadOnlyFile(_rutaArchivo)));
        await Shell.Current.GoToAsync("..");
#endif
    }

    private async Task RenderizarYMostrarAsync()
    {
        if (string.IsNullOrEmpty(_rutaArchivo)) return;

        _renderizando = true;
        OverlayCarga.IsVisible = true;
        try
        {
            _paginas = await _render.RenderizarPaginasAsync(
                _rutaArchivo, _anchoPxObjetivo, _grados, _cts.Token);

            ConstruirPaginas();
            OverlayCarga.IsVisible = false;
        }
        catch (OperationCanceledException)
        {
            // Se cerró la página a media renderizada: nada que hacer.
        }
        catch (Exception ex)
        {
            _logs.Error($"[VisorPdfPropio] Error al renderizar: {ex.Message}");
            OverlayCarga.IsVisible = false;
            await _toast.MostrarAsync("No se pudo mostrar el documento.", ToastIcono.Error, ToastPosicion.Bottom);
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            _renderizando = false;
        }
    }

    private void ConstruirPaginas()
    {
        ContenedorPaginas.Children.Clear();
        _altoContenidoBase = ContenedorPaginas.Padding.VerticalThickness;

        for (int i = 0; i < _paginas.Count; i++)
        {
            var pagina = _paginas[i];

            // Copia local por iteración: la factory del stream se invoca
            // varias veces y debe crear siempre un stream nuevo.
            var bytes = pagina.Jpeg;

            var imagen = new Image
            {
                Aspect = Aspect.AspectFit,
                WidthRequest = _anchoBaseDips,
                HeightRequest = _anchoBaseDips / pagina.Aspecto,
                Source = ImageSource.FromStream(() => new MemoryStream(bytes))
            };
            ContenedorPaginas.Children.Add(imagen);

            _altoContenidoBase += imagen.HeightRequest;
            if (i > 0) _altoContenidoBase += ContenedorPaginas.Spacing;
        }

        PaginaPill.IsVisible = _paginas.Count > 1;
        PaginaLabel.Text = $"1 / {_paginas.Count}";

        // El posicionamiento inicial (arriba del documento) necesita las
        // dimensiones del lienzo, disponibles después del pase de layout.
        Dispatcher.Dispatch(IrAlInicio);
    }

    // ── Zoom y paneo (transforms con clamp) ──────────────────────────────────

    private double AnchoVista() => Lienzo.Width > 0 ? Lienzo.Width : Width;
    private double AltoVista() => Lienzo.Height > 0 ? Lienzo.Height : Height;

    private double MaxTx() => Math.Max(0, (_anchoBaseDips * _zoom - AnchoVista()) / 2);
    private double MaxTy() => Math.Max(0, (_altoContenidoBase * _zoom - AltoVista()) / 2);

    private void IrAlInicio()
    {
        _tx = 0;
        _ty = MaxTy(); // contenido centrado: +MaxTy deja visible el inicio
        AplicarTransformaciones();
    }

    private void AplicarTransformaciones()
    {
        _tx = Math.Clamp(_tx, -MaxTx(), MaxTx());
        _ty = Math.Clamp(_ty, -MaxTy(), MaxTy());

        ContenedorPaginas.Scale = _zoom;
        ContenedorPaginas.TranslationX = _tx;
        ContenedorPaginas.TranslationY = _ty;

        ActualizarPill();
    }

    private void CambiarZoom(double nuevoZoom)
    {
        nuevoZoom = Math.Clamp(nuevoZoom, ZoomMin, ZoomMax);
        if (Math.Abs(nuevoZoom - _zoom) < 0.001) return;

        // Escala alrededor del centro: la traslación crece con el zoom para
        // que el punto al centro de la vista se quede en su lugar.
        double factorRelativo = nuevoZoom / _zoom;
        _zoom = nuevoZoom;
        _tx *= factorRelativo;
        _ty *= factorRelativo;

        AplicarTransformaciones();
    }

    private void OnZoomIn(object? sender, TappedEventArgs e)
        => CambiarZoom(_zoom * ZoomPaso);

    private void OnZoomOut(object? sender, TappedEventArgs e)
        => CambiarZoom(_zoom / ZoomPaso);

    private void OnDobleTap(object? sender, TappedEventArgs e)
        => CambiarZoom(_zoom > 1.01 ? 1 : 2);

    private void OnPinch(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (_renderizando || _paginas.Count == 0) return;

        switch (e.Status)
        {
            case GestureStatus.Started:
                _pinchActivo = true;
                _zoomInicioPinch = _zoom;
                _pinchAcumulado = 1.0;
                break;

            case GestureStatus.Running:
                _pinchAcumulado *= e.Scale;
                CambiarZoom(_zoomInicioPinch * _pinchAcumulado);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _pinchActivo = false;
                break;
        }
    }

    private void OnPan(object? sender, PanUpdatedEventArgs e)
    {
        if (_pinchActivo || _renderizando || _paginas.Count == 0) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panInicioX = _tx;
                _panInicioY = _ty;
                break;

            case GestureStatus.Running:
                _tx = _panInicioX + e.TotalX;
                _ty = _panInicioY + e.TotalY;
                AplicarTransformaciones();
                break;
        }
    }

    // ── Rotación / restaurar ─────────────────────────────────────────────────

    private async void OnRotarIzquierda(object? sender, TappedEventArgs e)
        => await RotarAsync(-90);

    private async void OnRotarDerecha(object? sender, TappedEventArgs e)
        => await RotarAsync(90);

    private async Task RotarAsync(int delta)
    {
        if (_renderizando) return;
        _grados = ((_grados + delta) % 360 + 360) % 360;
        await RenderizarYMostrarAsync();
    }

    private async void OnRestaurar(object? sender, TappedEventArgs e)
    {
        if (_renderizando) return;

        _zoom = 1;
        if (_grados != 0)
        {
            _grados = 0;
            await RenderizarYMostrarAsync(); // ConstruirPaginas ya reposiciona
        }
        else
        {
            IrAlInicio();
        }
    }

    // ── Indicador de página ──────────────────────────────────────────────────

    private void ActualizarPill()
    {
        if (_paginas.Count < 2) return;

        // Punto del contenido (en dips base) visible al centro de la vista.
        double centroContenido = _altoContenidoBase / 2 - _ty / _zoom;

        double borde = ContenedorPaginas.Padding.Top;
        int actual = 1;
        for (int i = 0; i < ContenedorPaginas.Children.Count; i++)
        {
            if (ContenedorPaginas.Children[i] is not Image imagen) continue;
            if (borde <= centroContenido) actual = i + 1;
            borde += imagen.HeightRequest + ContenedorPaginas.Spacing;
        }

        PaginaLabel.Text = $"{actual} / {_paginas.Count}";
    }

    // ── Acciones ─────────────────────────────────────────────────────────────

    private async void OnCompartir(object? sender, TappedEventArgs e)
    {
        if (_ocupado || string.IsNullOrEmpty(_rutaArchivo)) return;
        _ocupado = true;
        try
        {
            // Se comparte el PDF original, no las imágenes renderizadas.
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Compartir PDF",
                File = new ShareFile(_rutaArchivo)
            });
        }
        catch (Exception ex)
        {
            _logs.Error($"[VisorPdfPropio] Error al compartir: {ex.Message}");
            await _toast.MostrarAsync("No se pudo compartir el documento.", ToastIcono.Error, ToastPosicion.Bottom);
        }
        finally
        {
            _ocupado = false;
        }
    }

    private async void OnDescargar(object? sender, TappedEventArgs e)
    {
        if (_ocupado || string.IsNullOrEmpty(_rutaArchivo)) return;
        _ocupado = true;
        try
        {
            using var stream = File.OpenRead(_rutaArchivo);
            var resultado = await FileSaver.Default.SaveAsync(_nombreArchivo, stream, CancellationToken.None);

            if (resultado.IsSuccessful)
            {
                await _toast.MostrarAsync("Documento guardado.", ToastIcono.Info, ToastPosicion.Bottom);
            }
            else if (resultado.Exception is not OperationCanceledException &&
                     !(resultado.Exception?.Message.Contains("cancel", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                _logs.Error($"[VisorPdfPropio] Error al guardar: {resultado.Exception?.Message}");
                await _toast.MostrarAsync("No se pudo guardar el documento.", ToastIcono.Error, ToastPosicion.Bottom);
            }
        }
        catch (Exception ex)
        {
            _logs.Error($"[VisorPdfPropio] Error al guardar: {ex.Message}");
            await _toast.MostrarAsync("No se pudo guardar el documento.", ToastIcono.Error, ToastPosicion.Bottom);
        }
        finally
        {
            _ocupado = false;
        }
    }
}
