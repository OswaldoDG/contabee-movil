namespace ContaBee.Models.Configuracion;

public enum TipoConfiguracion
{
    Produccion = 0,
    DebugLocal = 1,
    Personalizada = 2,
}

public class ConfiguracionApp
{
    required public string UrlIdentityToken { get; set; }
    required public string UrlIdentity { get; set; }
    required public string UrlCrm { get; set; }
    required public string UrlTranscript { get; set; }
    required public string UrlEcommerce { get; set; }
}
