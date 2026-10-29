using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Ollama;
using Xunit;

namespace Bitcraft.UnitTests.Application.Ollama;

public class OllamaGenerationFeaturesTests
{
    [Fact]
    public async Task GenerateTextHandler_WithEmptyPrompt_ReturnsFailure()
    {
        var handler = new OllamaGenerationFeatures.GenerateTextHandler(new StubLlmGenerationProvider());

        var result = await handler.Handle(
            new OllamaGenerationFeatures.GenerateTextCommand("", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ollama.PromptRequired", result.Error.Code);
    }

    [Fact]
    public async Task GenerateTextHandler_WithWhitespaceModel_ReturnsFailure()
    {
        var handler = new OllamaGenerationFeatures.GenerateTextHandler(new StubLlmGenerationProvider());

        var result = await handler.Handle(
            new OllamaGenerationFeatures.GenerateTextCommand("Explain Docker", "   "),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ollama.ModelInvalid", result.Error.Code);
    }

    [Fact]
    public async Task GenerateTextHandler_WithValidPrompt_CallsProviderWithTrimmedValues()
    {
        var provider = new StubLlmGenerationProvider();
        var handler = new OllamaGenerationFeatures.GenerateTextHandler(provider);

        var result = await handler.Handle(
            new OllamaGenerationFeatures.GenerateTextCommand(" Explain Docker ", " llama3.2 "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Explain Docker", provider.Prompt);
        Assert.Equal("llama3.2", provider.Model);
    }

    private sealed class StubLlmGenerationProvider : ILlmGenerationProvider
    {
        public string? Prompt { get; private set; }
        public string? Model { get; private set; }

        public Task<Result<LlmGenerationResult>> GenerateAsync(
            string prompt,
            string? model,
            CancellationToken cancellationToken = default)
        {
            Prompt = prompt;
            Model = model;
            return Task.FromResult(Result.Success(new LlmGenerationResult(
                model ?? "llama3.2",
                "Generated text",
                Done: true)));
        }
    }
}
