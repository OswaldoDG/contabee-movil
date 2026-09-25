using ContaBee.Models.Configuracion;
namespace ContaBee.Services;

/// <summary>
/// Configuración de la app. Para cambiar de ambiente basta cambiar
/// <see cref="AMBIENTE_ACTIVO"/>. <see cref="Actual"/> contiene las URLs
/// de cada servicio para toda la app.
/// </summary>
public static class ServicioConfiguracion
{
    private const string URL_BASE_PRODUCCION = "https://api.contabee.mx";
    private const string URL_BASE_DESARROLLO = "https://apidev.contabee.mx";

    /// <summary>Ambiente de toda la app: Local, Desarrollo o Produccion.</summary>
    private const TipoConfiguracion AMBIENTE_ACTIVO = TipoConfiguracion.Desarrollo;

    /// <summary>Configuración activa de la app.</summary>
    public static ConfiguracionApp Actual { get; } = ObtieneConfiguracion(AMBIENTE_ACTIVO);

    private static ConfiguracionApp ObtieneConfiguracion(TipoConfiguracion tipoConfiguracion)
    {
        switch (tipoConfiguracion)
        {
            case TipoConfiguracion.Local:
                // Local cada servicio corre en su propio puerto, sin path /api.
                return new ConfiguracionApp
                {
                    UrlBase = "https://localhost",
                    UrlEcommerce = "https://localhost:8006/",
                    UrlCrm = "https://localhost:8002/",
                    UrlIdentity = "https://localhost:7001/",
                    UrlTranscript = "https://localhost:8004/"
                };

            case TipoConfiguracion.Desarrollo:
                return ConfiguracionApp.DesdeUrlBase(URL_BASE_DESARROLLO);

            case TipoConfiguracion.Produccion:
                return ConfiguracionApp.DesdeUrlBase(URL_BASE_PRODUCCION);

            default:
                throw new ArgumentOutOfRangeException(nameof(tipoConfiguracion), tipoConfiguracion, "Ambiente no configurado.");
        }
    }
}
