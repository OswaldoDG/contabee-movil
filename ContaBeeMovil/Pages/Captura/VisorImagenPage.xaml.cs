namespace ContaBeeMovil.Pages.Captura;

[QueryProperty(nameof(ImagePath), "path")]
public partial class VisorImagenPage : ContentPage
{
    private double _currentScale = 1;
    private double _xOffset;
    private double _yOffset;
    private double _startX;
    private double _startY;

    public VisorImagenPage()
    {
        InitializeComponent();
    }

    public string ImagePath
    {
        set => ImgVisor.Source = ImageSource.FromFile(value);
    }

    // ── Pan para desplazar cuando hay zoom ───────────────────────────────────

    private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (_currentScale <= 1) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _startX = _xOffset;
                _startY = _yOffset;
                break;

            case GestureStatus.Running:
                var maxX = ImgVisor.Width  * (_currentScale - 1) / 2;
                var maxY = ImgVisor.Height * (_currentScale - 1) / 2;
                _xOffset = Math.Clamp(_startX + e.TotalX, -maxX, maxX);
                _yOffset = Math.Clamp(_startY + e.TotalY, -maxY, maxY);
                ImgVisor.TranslationX = _xOffset;
                ImgVisor.TranslationY = _yOffset;
                break;

            case GestureStatus.Completed:
                _startX = _xOffset;
                _startY = _yOffset;
                break;
        }
    }

    // ── Doble tap: alternar zoom 1x / 2.5x ───────────────────────────────────

    private async void OnDoubleTap(object sender, TappedEventArgs e)
    {
        _currentScale = _currentScale > 1 ? 1 : 2.5;
        await ImgVisor.ScaleToAsync(_currentScale, 250, Easing.CubicInOut);

        if (_currentScale == 1)
        {
            await ImgVisor.TranslateToAsync(0, 0, 250, Easing.CubicInOut);
            _xOffset = 0;
            _yOffset = 0;
        }
    }
}
