using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using MediatR;

namespace Bitcraft.Application.Features.Ollama;

public static class OllamaGenerationFeatures
{
    public sealed record GenerateTextCommand(
        string Prompt,
        string? Model) : IRequest<Result<LlmGenerationResult>>;

    public sealed class GenerateTextHandler(ILlmGenerationProvider generationProvider)
        : IRequestHandler<GenerateTextCommand, Result<LlmGenerationResult>>
    {
        public Task<Result<LlmGenerationResult>> Handle(
            GenerateTextCommand request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return Task.FromResult(Result.Failure<LlmGenerationResult>(new Error(
                    "Ollama.PromptRequired",
                    "Prompt is required.")));
            }

            if (request.Model is not null && string.IsNullOrWhiteSpace(request.Model))
            {
                return Task.FromResult(Result.Failure<LlmGenerationResult>(new Error(
                    "Ollama.ModelInvalid",
                    "Model cannot be empty when provided.")));
            }

            var prompt = request.Prompt.Trim();
            var model = request.Model?.Trim();

            return generationProvider.GenerateAsync(prompt, model, cancellationToken);
        }
    }
}
