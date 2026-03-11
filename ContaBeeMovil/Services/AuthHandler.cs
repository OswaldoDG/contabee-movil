using Microsoft.Extensions.DependencyInjection;

namespace ContaBeeMovil.Services;

public class AuthHandler : DelegatingHandler
{
    private static readonly string[] _rutasPublicas =
    [
        "/usuario/registro",
        "/usuario/contrasena/recuperar",
        "/usuario/contrasena/restablecer",
        "/usuario/registro/confirmar"
    ];

    private readonly IServiceProvider _serviceProvider;

    public AuthHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        bool esPublica = _rutasPublicas.Any(r => path.StartsWith(r));

        if (!esPublica)
        {
            var sesion = _serviceProvider.GetRequiredService<IServicioSesion>();
            var token = await sesion.LeeAccessTokenAsync();
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}