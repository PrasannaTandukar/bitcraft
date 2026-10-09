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
            var trimmed = model.ApiKey.Trim();
            // An explicit ref:/secret: marker denotes a logical reference. It resolves
            // through ModelProviders:Secrets:<reference> or fails closed before transport.
            if (trimmed.StartsWith("ref:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("secret:", StringComparison.OrdinalIgnoreCase))
            {
                var reference = trimmed[(trimmed.IndexOf(':') + 1)..].Trim();
                var marked = configuration[$"ModelProviders:Secrets:{reference}"];
                return string.IsNullOrWhiteSpace(marked)
                    ? Result.Failure<string?>(new Error("TutorAgent.SecretNotFound", "The runtime API key reference could not be resolved."))
                    : Result.Success<string?>(marked.Trim());
            }
            // Backward compatibility: a bare stored reference name resolves through
            // ModelProviders:Secrets:<name> when deployment configuration defines it.
            // Any other value is a raw key and is forwarded verbatim.
            var resolved = configuration[$"ModelProviders:Secrets:{trimmed}"];
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return Result.Success<string?>(resolved.Trim());
            }
            return Result.Success<string?>(trimmed);
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
