namespace Bitcraft.Application.Common.Models;

public static class EmbeddingProviders
{
    public const string Ollama = "Ollama";
    public const string OpenAI = "OpenAI";
    public const string OpenAICompatible = "OpenAICompatible";
    public const string LlamaCpp = "LlamaCpp";

    public static readonly IReadOnlyCollection<string> All = [Ollama, OpenAI, OpenAICompatible, LlamaCpp];

    public static string? Normalize(string? provider) => string.IsNullOrWhiteSpace(provider)
        ? null
        : All.FirstOrDefault(x => string.Equals(x, provider.Trim(), StringComparison.OrdinalIgnoreCase));
}
