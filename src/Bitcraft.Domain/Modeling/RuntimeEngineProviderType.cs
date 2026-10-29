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
