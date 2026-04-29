using System.Diagnostics;
using System.Net.Http.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Infrastructure.ExternalServices.Ollama;

/// <summary>
/// Health probe for an Ollama-compatible model endpoint. Calls
/// <c>GET {endpoint}/api/tags</c> and verifies the configured model is
/// present in the returned tag list.
/// </summary>
public class OllamaHealthCheck(IHttpClientFactory httpClientFactory) : IModelConnectionHealthCheck
{
    public async Task<Result<ModelHealthStatus>> CheckAsync(ModelCatalogEntry catalogEntry, CancellationToken cancellationToken = default)
    {
        if (catalogEntry.ProviderType != ModelProviderType.Ollama)
        {
            return Result.Failure<ModelHealthStatus>(new Error(
                "ModelConnection.UnsupportedProvider",
                $"Provider '{catalogEntry.ProviderType}' is not supported by the Ollama health probe."));
        }

        if (!Uri.TryCreate(catalogEntry.Endpoint, UriKind.Absolute, out var baseUri))
        {
            return Result.Failure<ModelHealthStatus>(new Error(
                "ModelConnection.EndpointInvalid",
                "Endpoint is not a valid absolute URI."));
        }

        var client = httpClientFactory.CreateClient(nameof(OllamaHealthCheck));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var tagsUri = new Uri(baseUri, "api/tags");
            using var response = await client.GetAsync(tagsUri, cancellationToken);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return Result.Success(new ModelHealthStatus(
                    IsReachable: false,
                    ProviderType: catalogEntry.ProviderType.ToString(),
                    Endpoint: catalogEntry.Endpoint,
                    ModelName: catalogEntry.ModelName,
                    ModelAvailable: false,
                    LatencyMs: stopwatch.ElapsedMilliseconds,
                    Detail: $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"));
            }

            var payload = await response.Content.ReadFromJsonAsync<TagsResponse>(cancellationToken: cancellationToken);
            var modelAvailable = payload?.Models?.Any(m =>
                string.Equals(m.Name, catalogEntry.ModelName, StringComparison.OrdinalIgnoreCase) ||
                (m.Name?.StartsWith(catalogEntry.ModelName + ":", StringComparison.OrdinalIgnoreCase) ?? false)) ?? false;

            return Result.Success(new ModelHealthStatus(
                IsReachable: true,
                ProviderType: catalogEntry.ProviderType.ToString(),
                Endpoint: catalogEntry.Endpoint,
                ModelName: catalogEntry.ModelName,
                ModelAvailable: modelAvailable,
                LatencyMs: stopwatch.ElapsedMilliseconds,
                Detail: modelAvailable ? null : "Model not present in tag list."));
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Result.Success(new ModelHealthStatus(
                IsReachable: false,
                ProviderType: catalogEntry.ProviderType.ToString(),
                Endpoint: catalogEntry.Endpoint,
                ModelName: catalogEntry.ModelName,
                ModelAvailable: false,
                LatencyMs: stopwatch.ElapsedMilliseconds,
                Detail: ex.GetType().Name + ": " + ex.Message));
        }
    }

    private sealed record TagsResponse(List<TagsModel>? Models);
    private sealed record TagsModel(string? Name);
}
