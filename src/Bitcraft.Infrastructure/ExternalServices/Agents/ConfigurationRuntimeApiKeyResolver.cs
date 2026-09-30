using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Modeling;
using Microsoft.Extensions.Configuration;

namespace Bitcraft.Infrastructure.ExternalServices.Agents;

/// <summary>Resolves runtime credentials: the engine's stored key first, then deployment configuration fallbacks.</summary>
public sealed class ConfigurationRuntimeApiKeyResolver(IConfiguration configuration) : IRuntimeApiKeyResolver
{
    public Result<string?> Resolve(TutorAgentModelConfiguration model)
    {
        if (!string.IsNullOrWhiteSpace(model.ApiKey))
        {
            return Result.Success<string?>(model.ApiKey.Trim());
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
