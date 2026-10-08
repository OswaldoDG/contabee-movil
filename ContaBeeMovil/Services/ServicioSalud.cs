using ContaBee.Services;

namespace ContaBeeMovil.Services;

public interface IServicioSalud
{
    Task<bool> VerificarServiciosAsync();
}

public class ServicioSalud(IHttpClientFactory factory) : IServicioSalud
{
    private static string[] Endpoints()
    {
        var config = ServicioConfiguracion.Actual;
        return
        [
            $"{config.UrlIdentity}health",
            $"{config.UrlCrm}health",
            $"{config.UrlTranscript}health",
            $"{config.UrlEcommerce}health",
        ];
    }

    public async Task<bool> VerificarServiciosAsync()
    {
        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(6);

        var tareas = Endpoints().Select(async url =>
        {
            try
            {
                using var res = await client.GetAsync(url);
                return res.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        });

        var resultados = await Task.WhenAll(tareas);
        return resultados.All(ok => ok);
    }
}
