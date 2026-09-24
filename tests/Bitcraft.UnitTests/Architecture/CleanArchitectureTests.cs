using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.ExternalServices.Benchmarking;
using Xunit;

namespace Bitcraft.UnitTests.Architecture;

public sealed class CleanArchitectureTests
{
    private static readonly string[] ForbiddenFrameworkAssemblies =
    [
        "Microsoft.Agents.AI",
        "Microsoft.Extensions.AI",
        "OllamaSharp",
        "OpenAI",
        "Anthropic"
    ];

    [Fact]
    public void Domain_DoesNotReferenceApplicationApiInfrastructureOrAiFrameworks()
    {
        AssertNoReferences(typeof(Tutor).Assembly,
            ["Bitcraft.Application", "Bitcraft.Infrastructure", "Bitcraft.Api", .. ForbiddenFrameworkAssemblies]);
    }

    [Fact]
    public void Application_DoesNotReferenceApiInfrastructureOrAiFrameworks()
    {
        AssertNoReferences(typeof(ITutorRuntime).Assembly,
            ["Bitcraft.Infrastructure", "Bitcraft.Api", .. ForbiddenFrameworkAssemblies]);
    }

    [Fact]
    public void BenchmarkOutputGenerator_DependsOnCanonicalTutorRuntime()
    {
        var parameters = Assert.Single(typeof(TutorBenchmarkOutputGenerator).GetConstructors()).GetParameters();
        Assert.Contains(parameters, value => value.ParameterType == typeof(ITutorRuntime));
        Assert.DoesNotContain(parameters, value => value.ParameterType == typeof(ITutorAgentExecutor));
    }

    [Fact]
    public void LegacyGenerationAbstractions_AreAbsent()
    {
        var application = typeof(ITutorRuntime).Assembly;
        Assert.Null(application.GetType("Bitcraft.Application.Common.Interfaces.ILlmGenerationProvider"));
        Assert.Null(application.GetType("Bitcraft.Application.Common.Models.LlmGenerationRequest"));
        Assert.Null(application.GetType("Bitcraft.Application.Features.Rag.RagAnswerFeatures"));
        Assert.Null(application.GetType("Bitcraft.Application.Features.Ollama.OllamaGenerationFeatures"));
        Assert.Null(typeof(ToolDefinition).GetProperty("ExecutionUrl"));
    }

    private static void AssertNoReferences(System.Reflection.Assembly assembly,
        IReadOnlyCollection<string> forbidden)
    {
        var references = assembly.GetReferencedAssemblies().Select(value => value.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(forbidden, references.Contains);
    }
}
