using ContaBee.Models.Configuracion;
namespace ContaBee.Services;

public static class ServicioConfiguracion
{
    public static ConfiguracionApp ObtieneConfiguracion(TipoConfiguracion tipoConfiguracion)
    {
        switch (tipoConfiguracion)
        {
            case TipoConfiguracion.DebugLocal:
                return new ConfiguracionApp
                {
                    UrlIdentityToken = "https://localhost:7001/",
                    UrlEcommerce = "https://localhost:8006/",
                    UrlCrm = "https://localhost:8002/",
                    UrlIdentity = "https://localhost:7001/",
                    UrlTranscript = "https://localhost:8004/"
                };

            default:
                return new ConfiguracionApp
                {
                    UrlIdentityToken = "https://api.contabee.mx/api/identity/",
                    UrlEcommerce = "https://api.contabee.mx/api/ecommerce/",
                    UrlCrm = "https://api.contabee.mx/api/crm/",
                    UrlIdentity = "https://api.contabee.mx/api/identity/",
                    UrlTranscript = "https://api.contabee.mx/api/transcript/"
                };
        }


    }
}
