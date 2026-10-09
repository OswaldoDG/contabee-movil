namespace ContaBeeMovil.Services;

public interface IServicioSalud
{
    Task<bool> VerificarServiciosAsync();
}

public class ServicioSalud(IHttpClientFactory factory) : IServicioSalud
{
    private static readonly string[] _endpoints =
    [
        "https://api.contabee.mx/api/identity/health",
        "https://api.contabee.mx/api/crm/health",
        "https://api.contabee.mx/api/transcript/health",
        "https://api.contabee.mx/api/ecommerce/health",
    ];

    public async Task<bool> VerificarServiciosAsync()
    {
        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(6);

        var tareas = _endpoints.Select(async url =>
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
