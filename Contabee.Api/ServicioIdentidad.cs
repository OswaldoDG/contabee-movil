using Contabee.Api.abstractions;
using Contabee.Api.Identidad;

namespace Contabee.Api;

public class ServicioIdentidad(HttpClient httpClient) : IServicioIdentidad
{
    private readonly ServicioIdentidadClient servicioIdentidad = new (httpClient.BaseAddress!.ToString(), httpClient);

    public async Task<Respuesta> Registrar(RegisterViewModel request )
    {
        Respuesta r = new ();

        try
		{
			await servicioIdentidad.RegistroAsync(true, request);
            r.Ok = true;
        }
		catch (Exception ex)
		{
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Registrar");
		}

        return r;
    }
}
