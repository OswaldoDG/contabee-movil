using CommunityToolkit.Maui.Views;

namespace ContaBeeMovil.Helpers;

internal static class VistaExtensions
{
    internal static Layout? ObtenerLayoutRaizPagina(this Element elemento)
    {
        Element? actual = elemento;
        Layout? ultimoLayoutEncontrado = null;

        while (actual is not null)
        {
            if (actual is Layout layoutActual)
                ultimoLayoutEncontrado = layoutActual;

            if (actual is Popup popup)
            {
                var paginaActiva = ObtenerPaginaActiva();
                if (paginaActiva?.Content is Layout layoutPaginaActiva)
                    return layoutPaginaActiva;

                if (popup.Content is Layout popupLayout)
                    return popupLayout;

                if (popup.Content is ContentView contentView && contentView.Content is Layout contentLayout)
                    return contentLayout;

                return ultimoLayoutEncontrado;
            }

            if (actual is ContentPage pagina && pagina.Content is Layout layout)
                return layout;

            actual = actual.Parent;
        }

        return ultimoLayoutEncontrado;
    }

    private static ContentPage? ObtenerPaginaActiva()
    {
        Page? page = Application.Current?.Windows.FirstOrDefault()?.Page;

        if (page is Shell shell)
            page = shell.CurrentPage;

        if (page is NavigationPage navigationPage)
            page = navigationPage.CurrentPage;

        if (page is TabbedPage tabbedPage)
            page = tabbedPage.CurrentPage;

        return page as ContentPage;
    }

    internal static ContentPage? ObtenerPagina(this Element elemento)
    {
        var actual = elemento;
        while (actual is not null)
        {
            if (actual is ContentPage pagina)
                return pagina;
            actual = actual.Parent;
        }
        return null;
    }
}
