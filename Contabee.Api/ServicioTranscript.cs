using System.Text.Json;
using Contabee.Api.abstractions;


namespace Contabee.Api;

public class ServicioTranscript(HttpClient httpClient) : IServicioTranscript
{
    private readonly ServicioTranscriptClient servicioTranscript = new (httpClient.BaseAddress!.ToString(), httpClient);
    
}
