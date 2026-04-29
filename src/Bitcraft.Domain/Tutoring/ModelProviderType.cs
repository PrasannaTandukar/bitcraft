namespace Bitcraft.Domain.Tutoring;

/// <summary>Supported LLM provider types for a model connection.</summary>
public enum ModelProviderType
{
    /// <summary>Local Ollama-compatible runtime.</summary>
    Ollama = 0,
    /// <summary>OpenAI hosted API.</summary>
    OpenAI = 1,
    /// <summary>Custom / self-hosted provider with bring-your-own configuration.</summary>
    Custom = 99
}
