using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Infrastructure.ExternalServices.Benchmarking;
using Xunit;

namespace Bitcraft.InfrastructureIntegrationTests.Benchmarking;

public class TutorBenchmarkOutputGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_DelegatesCompleteConfigurationSelectionToSharedRuntime()
    {
        var runtime = new RecordingTutorRuntime();
        var generator = new TutorBenchmarkOutputGenerator(runtime);
        var tutorId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = await generator.GenerateAsync(new TutorBenchmarkGenerationRequest(
            tutorId,
            revisionId,
            snapshotId,
            "What is inverse kinematics?",
            ContextOverride: "This must not bypass the configured runtime.",
            UseTutorKnowledge: false,
            TopK: 3,
            userId));

        Assert.True(result.IsSuccess);
        Assert.NotNull(runtime.Request);
        Assert.Equal(tutorId, runtime.Request!.TutorId);
        Assert.Equal(revisionId, runtime.Request.TutorRevisionId);
        Assert.Equal(snapshotId, runtime.Request.TutorConfigurationSnapshotId);
        Assert.Equal(userId, runtime.Request.UserId);
        Assert.Equal(TutorRuntimeExecutionMode.Benchmark, runtime.Request.ExecutionMode);
        Assert.False(runtime.Request.UseKnowledge);
        Assert.Null(runtime.Request.ConversationId);
        Assert.False(runtime.Request.PersistConversation);
        Assert.Equal(3, runtime.Request.TopK);
        Assert.Equal("This must not bypass the configured runtime.", result.Value.ContextText);
        Assert.Equal("configured-model", result.Value.ModelUsed);
        Assert.Equal(revisionId, result.Value.TutorRevisionId);
        Assert.Equal(snapshotId, result.Value.TutorConfigurationSnapshotId);
        Assert.False(result.Value.KnowledgeUsed);
    }

    private sealed class RecordingTutorRuntime : ITutorRuntime
    {
        public TutorRuntimeRequest? Request { get; private set; }

        public Task<Result<TutorRuntimeResult>> ExecuteAsync(
            TutorRuntimeRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(Result.Success(new TutorRuntimeResult(
                request.TutorId,
                "Robotics Tutor",
                request.TutorRevisionId!.Value,
                3,
                request.TutorConfigurationSnapshotId!.Value,
                "Generated answer",
                new TutorRuntimeModelMetadata(
                    Guid.NewGuid(),
                    "Configured Model",
                    "configured-model",
                    Guid.NewGuid(),
                    "Ollama"),
                null,
                request.UseKnowledge,
                [],
                new TutorRuntimeUsageMetadata(12),
                "Runtime reference material")));
        }
    }
}
