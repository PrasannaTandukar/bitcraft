using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Modeling;
using Microsoft.Extensions.Configuration;

namespace Bitcraft.Infrastructure.ExternalServices.Agents;

/// <summary>Resolves logical references through deployment-supplied IConfiguration providers.</summary>
public sealed class ConfigurationRuntimeApiKeyResolver(IConfiguration configuration) : IRuntimeApiKeyResolver
{
    public Result<string?> Resolve(TutorAgentModelConfiguration model)
    {
        if (!string.IsNullOrWhiteSpace(model.ApiKeySecretRef))
        {
            var resolved = configuration[$"ModelProviders:Secrets:{model.ApiKeySecretRef.Trim()}"];
            return string.IsNullOrWhiteSpace(resolved)
                ? Result.Failure<string?>(new Error("TutorAgent.SecretNotFound", "The runtime API key reference could not be resolved."))
                : Result.Success<string?>(resolved.Trim());
        }

        var provider = model.ProviderType switch
        {
            RuntimeEngineProviderType.OpenAICompatible or RuntimeEngineProviderType.vLLM or RuntimeEngineProviderType.LlamaCpp => "OpenAI",
            _ => model.ProviderType.ToString()
        };
        var key = configuration[$"ModelProviders:{provider}:ApiKey"]
            ?? configuration[$"{provider.ToUpperInvariant()}_API_KEY"];
        return Result.Success<string?>(string.IsNullOrWhiteSpace(key) ? null : key.Trim());
    }
}
