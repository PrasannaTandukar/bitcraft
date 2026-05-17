namespace Bitcraft.Api.Contracts.Ollama;

public sealed record GenerateOllamaRequest(
    string Prompt,
    string? Model = null);

public sealed record GenerateOllamaResponse(
    string Model,
    string Response,
    bool Done);
