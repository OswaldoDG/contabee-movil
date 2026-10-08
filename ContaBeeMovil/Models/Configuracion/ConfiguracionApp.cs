namespace ContaBee.Models.Configuracion;

public enum TipoConfiguracion
{
    Produccion = 0,
    Local = 1,
    Personalizada = 2,
    Desarrollo = 3,
}

public class ConfiguracionApp
{
    /// <summary>URL del gateway en Desarrollo/Produccion. En Local cada servicio tiene su propio puerto.</summary>
    required public string UrlBase { get; set; }
    required public string UrlIdentity { get; set; }
    required public string UrlCrm { get; set; }
    required public string UrlTranscript { get; set; }
    required public string UrlEcommerce { get; set; }

    /// <summary>Construye la configuración derivando todas las URLs de servicio de la URL base.</summary>
    public static ConfiguracionApp DesdeUrlBase(string urlBase) => new()
    {
        UrlBase = urlBase,
        UrlIdentity = $"{urlBase}/api/identity/",
        UrlCrm = $"{urlBase}/api/crm/",
        UrlTranscript = $"{urlBase}/api/transcript/",
        UrlEcommerce = $"{urlBase}/api/ecommerce/",
    };
}
