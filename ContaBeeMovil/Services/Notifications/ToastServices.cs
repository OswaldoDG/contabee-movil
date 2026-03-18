
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using ContaBeeMovil.Helpers;
using Microsoft.Maui.Controls;

namespace ContaBeeMovil.Services.Notifications;

public enum ToastType { Success, Warning, Error }
public enum ToastPosition { Top, Center, Bottom }

public interface IToastService
{
    Task ShowAsync(string message, ToastType type = ToastType.Success,
                   int durationMs = 3000, ToastPosition position = ToastPosition.Top);
}

public class ToastService : IToastService
{
    public async Task ShowAsync(string message, ToastType type = ToastType.Success,
                                int durationMs = 3000, ToastPosition position = ToastPosition.Top)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = GetCurrentPage();
            if (page == null) return;

            // Si el contenido ya es un Grid overlay nuestro, lo reutilizamos
            // Si no, envolvemos UNA SOLA VEZ y ya no lo tocamos más
            Grid overlay;
            if (page.Content is Grid g && g.ClassId == "ToastOverlay")
            {
                overlay = g;
            }
            else
            {
                var originalContent = page.Content;
                overlay = new Grid { ClassId = "ToastOverlay" };
                overlay.Children.Add(originalContent);
                page.Content = overlay; // ← solo se asigna UNA vez
            }

            var cts = new CancellationTokenSource();
            var toast = BuildToast(message, type, position, cts);
            toast.Opacity = 0;
            toast.TranslationY = position == ToastPosition.Bottom ? 60 : -60;

            overlay.Children.Add(toast);

            // Animación entrada
            await Task.WhenAll(
                toast.FadeToAsync(1, 300),
                toast.TranslateToAsync(0, 0, 300, Easing.CubicIn)
            );

            try { await Task.Delay(durationMs, cts.Token); }
            catch (TaskCanceledException) { }

            // Animación salida
            double exitY = position == ToastPosition.Bottom ? 60 : -60;
            await Task.WhenAll(
                toast.FadeToAsync(0, 300),
                toast.TranslateToAsync(0, exitY, 300, Easing.CubicOut)
            );

            // Remover toast y restaurar contenido original
            if (overlay.Children.Contains(toast))
                overlay.Children.Remove(toast);
            if (overlay.Children.Count == 1)
            {
                var original = overlay.Children[0];
                overlay.Children.Clear();
                page.Content = (View)original;
            }
        });
    }

    private ContentPage? GetCurrentPage()
    {
        var window = Application.Current?.Windows.FirstOrDefault();

        return window?.Page switch
        {
            NavigationPage nav => nav.CurrentPage as ContentPage,
            Shell shell => shell.CurrentPage as ContentPage,
            TabbedPage tabbed => tabbed.CurrentPage as ContentPage,
            ContentPage cp => cp,
            _ => window?.Page as ContentPage
        };
    }

    private Grid BuildToast(string message, ToastType type, ToastPosition position, CancellationTokenSource cts)
    {
        var color = UIHelpers.GetColor("Primary");

        // Margen y alineación según posición
        var (verticalOptions, margin) = position switch
        {
            ToastPosition.Top => (LayoutOptions.Start, new Thickness(20, 50, 20, 0)),
            ToastPosition.Center => (LayoutOptions.Center, new Thickness(20, 0, 20, 0)),
            ToastPosition.Bottom => (LayoutOptions.End, new Thickness(20, 0, 20, 40)),
            _ => (LayoutOptions.Start, new Thickness(20, 50, 20, 0))
        };

        // Contenedor principal //Color.FromArgb("00FFFFFF")
        var frame = new Border
        {
            BackgroundColor = Color.FromArgb("00FFFFFF"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };
        
       
        // Grid interior: icono | mensaje | X
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing = 10,
            VerticalOptions = LayoutOptions.Center,
        };

        // Icono de tipo (círculo con símbolo)
        var iconFrame = new Frame
        {
            BackgroundColor = Colors.Black,
            CornerRadius = 14,
            WidthRequest = 28,
            HeightRequest = 28,
            Padding = 0,
            HasShadow = false,
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = type==ToastType.Success ? "✓" : "!",
                TextColor = Colors.White,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            }
        };
        // Mensaje
        var textColor = (type == ToastType.Success || type == ToastType.Error) ? color : Color.FromArgb("#1A1A1A");
        var label = new Label
        {
            Text = message,
            TextColor = textColor,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
        };

        // Botón cerrar
        var closeLabel = new Label
        {
            Text = "✕",
            TextColor = Color.FromArgb("#999999"),
            FontSize = 16,
            VerticalOptions = LayoutOptions.Center,
        };

        // Acento en esquina inferior derecha
        var accent =new BoxView
        {
            Color = color,
            CornerRadius = new CornerRadius(5, 5, 5, 5),
           BackgroundColor= Color.FromArgb("00FFFFFF"),
           Shadow = new Shadow() { Radius= 7f,Opacity=.75f},

        };

        // Acento en esquina inferior derecha
        var accent2 = new BoxView
        {
            Color = UIHelpers.GetColor("Background"),
            CornerRadius = new CornerRadius(5, 5, 5, 150),
            BackgroundColor = Color.FromArgb("00FFFFFF"),
            //WidthRequest=100,
           // HorizontalOptions = LayoutOptions.End,
            //VerticalOptions = LayoutOptions.End,

        };
        var stack = new Grid();


        // Wrapper con acento encima
        var wrapper = new Grid
        {
            Margin = margin,
            VerticalOptions = verticalOptions,
        };

        grid.Add(iconFrame, 0, 0);
        grid.Add(label, 1, 0);
        grid.Add(closeLabel, 2, 0);

        frame.Content = grid;

        // Apilar: accent2 abajo, frame encima
        stack.Children.Add(accent2);  // ← fondo
        stack.Children.Add(frame);
        stack.Padding = new Thickness(2, 2);// ← encima

        // El wrapper usa el stack
        wrapper.Children.Add(accent);
        wrapper.Children.Add(stack);

        // Tap para cerrar (cancela el delay para que ShowAsync termine)
        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) =>
        {
            cts.Cancel();
        };
        wrapper.GestureRecognizers.Add(tap);

        return wrapper;
    }
}