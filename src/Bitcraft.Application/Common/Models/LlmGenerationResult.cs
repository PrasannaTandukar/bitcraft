namespace Bitcraft.Application.Common.Models;

public sealed record LlmGenerationResult(
    string Model,
    string Response,
    bool Done);
