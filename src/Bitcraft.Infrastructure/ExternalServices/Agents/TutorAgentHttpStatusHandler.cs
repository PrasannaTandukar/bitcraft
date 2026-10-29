namespace Bitcraft.Infrastructure.ExternalServices.Agents;

/// <summary>Preserves HTTP status codes before OllamaSharp replaces them with body-only exceptions.</summary>
internal sealed class TutorAgentHttpStatusHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return response;
        var status = response.StatusCode;
        response.Dispose();
        throw new HttpRequestException("The configured provider rejected the request.", null, status);
    }
}
