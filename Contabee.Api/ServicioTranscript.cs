using System.Text.Json;
using Contabee.Api.abstractions;
using Contabee.Api.Transcript;


namespace Contabee.Api;

public class ServicioTranscript(HttpClient httpClient) : IServicioTranscript
{
    private readonly ServicioTranscriptClient servicioTranscript = new (httpClient.BaseAddress!.ToString(), httpClient);
    
}
