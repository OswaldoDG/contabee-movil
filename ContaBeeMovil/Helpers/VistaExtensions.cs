namespace ContaBeeMovil.Helpers;

internal static class VistaExtensions
{
    internal static Layout? ObtenerLayoutRaizPagina(this Element elemento)
    {
        var actual = elemento;
        while (actual.Parent is not null)
        {
            if (actual.Parent is ContentPage pagina && pagina.Content is Layout layout)
                return layout;
            actual = actual.Parent;
        }
        return null;
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
