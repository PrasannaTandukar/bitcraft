namespace Bitcraft.Application.Common.Models;

public static class EmbeddingProviders
{
    public const string Ollama = "Ollama";

    public static readonly IReadOnlyCollection<string> All = [Ollama];

    public static string? Normalize(string? provider) => string.IsNullOrWhiteSpace(provider)
        ? null
        : All.FirstOrDefault(x => string.Equals(x, provider.Trim(), StringComparison.OrdinalIgnoreCase));
}
