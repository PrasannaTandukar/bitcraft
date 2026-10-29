namespace Bitcraft.Domain.Modeling;

/// <summary>
/// Locked vocabulary of upstream model-runtime providers a <see cref="RuntimeEngine"/> may target (D11.a).
/// Persisted as <c>int</c> via <c>HasConversion&lt;int&gt;()</c>; values are part of the on-disk contract and must not be renumbered.
/// </summary>
public enum RuntimeEngineProviderType
{
    /// <summary>Local Ollama-compatible runtime.</summary>
    Ollama = 0,

    /// <summary>OpenAI hosted API.</summary>
    OpenAI = 1,

    /// <summary>Self-hosted or third-party endpoint that implements the OpenAI HTTP contract.</summary>
    OpenAICompatible = 2,

    /// <summary>vLLM inference server.</summary>
    vLLM = 3,

    /// <summary>llama.cpp HTTP server.</summary>
    LlamaCpp = 4,

    // --- Reserved (schema-locked, not surfaced in UI for freeze) ---

    /// <summary>Reserved: Azure OpenAI Service. Not surfaced in UI for the feature-freeze release.</summary>
    AzureOpenAI = 100,

    /// <summary>Reserved: Anthropic hosted API. Not surfaced in UI for the feature-freeze release.</summary>
    Anthropic = 101,

    /// <summary>Reserved: AWS Bedrock. Not surfaced in UI for the feature-freeze release.</summary>
    Bedrock = 102
}

/// <summary>
/// Wire dialect a generation or embedding request is routed to. Single source of truth for
/// provider-type routing (consolidation plan, OD-1): every call site maps through
/// <see cref="RuntimeEngineProviderTypeDialect.ToLlmDialect"/> /
/// <see cref="RuntimeEngineProviderTypeDialect.ToEmbeddingDialect"/> instead of maintaining its own list.
/// </summary>
public enum LlmDialect
{
    Ollama = 0,
    OpenAI = 1,
    Anthropic = 2,
    Unsupported = 3
}

/// <summary>
/// Shared provider-type to dialect mapping (consolidation plan, OD-1). The OpenAI dialect serves
/// hosted OpenAI and every OpenAI-contract runtime (OpenAICompatible, vLLM, LlamaCpp). Anthropic
/// is reserved but still routable for generation so existing engine rows keep functioning. It has
/// no embedding provider.
/// </summary>
public static class RuntimeEngineProviderTypeDialect
{
    /// <summary>Maps a provider type to its generation dialect.</summary>
    public static LlmDialect ToLlmDialect(this RuntimeEngineProviderType providerType) => providerType switch
    {
        RuntimeEngineProviderType.Ollama => LlmDialect.Ollama,
        RuntimeEngineProviderType.OpenAI or
        RuntimeEngineProviderType.OpenAICompatible or
        RuntimeEngineProviderType.vLLM or
        RuntimeEngineProviderType.LlamaCpp => LlmDialect.OpenAI,
        RuntimeEngineProviderType.Anthropic => LlmDialect.Anthropic,
        _ => LlmDialect.Unsupported
    };

    /// <summary>Maps a provider type to its embedding dialect (Ollama and the OpenAI dialect only).</summary>
    public static LlmDialect ToEmbeddingDialect(this RuntimeEngineProviderType providerType) => providerType switch
    {
        RuntimeEngineProviderType.Ollama => LlmDialect.Ollama,
        RuntimeEngineProviderType.OpenAI or
        RuntimeEngineProviderType.OpenAICompatible or
        RuntimeEngineProviderType.vLLM or
        RuntimeEngineProviderType.LlamaCpp => LlmDialect.OpenAI,
        _ => LlmDialect.Unsupported
    };
}
