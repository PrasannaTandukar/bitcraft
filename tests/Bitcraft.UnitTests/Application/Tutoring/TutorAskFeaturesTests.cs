using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Tutoring;
using Bitcraft.Domain.Tutoring;
using Bitcraft.UnitTests.Application.Tutoring.Fakes;
using Xunit;

namespace Bitcraft.UnitTests.Application.Tutoring;

public class TutorAskFeaturesTests
{
    [Fact]
    public async Task LegacyAsk_WithModelOverride_RejectsOverrideWithoutExecuting()
    {
        var setup = CreateSetup();

        var result = await setup.Handler.Handle(
            new TutorAskFeatures.AskTutorQuestionCommand("Hello", "llama3.2"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TutorRuntime.ModelOverrideNotAllowed", result.Error.Code);
        Assert.Null(setup.Runtime.Request);
    }

    [Fact]
    public async Task LegacyAsk_WithSingleActiveTutor_DelegatesToSharedRuntime()
    {
        var setup = CreateSetup();

        var result = await setup.Handler.Handle(
            new TutorAskFeatures.AskTutorQuestionCommand(
                "What is an encoder?",
                null,
                UseMemory: true,
                SaveToMemory: true,
                TopK: 4,
                History: [new TutorRuntimeConversationMessage("user", "We are discussing motors.")]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(setup.Runtime.Request);
        Assert.Equal(setup.Tutor.Id, setup.Runtime.Request!.TutorId);
        Assert.Equal(4, setup.Runtime.Request.TopK);
        Assert.Equal(TutorRuntimeExecutionMode.Interactive, setup.Runtime.Request.ExecutionMode);
        Assert.False(result.Value.MemorySaved);
        Assert.Contains("long-term memory is not enabled", result.Value.Message);
    }

    [Fact]
    public async Task LegacyAsk_WithoutUnambiguousTutor_ReturnsTutorRequired()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeTutorRepository();
        var handler = new TutorAskFeatures.AskTutorQuestionHandler(
            new RecordingRuntime(),
            repository,
            new FakeCurrentUserContext(userId));

        var result = await handler.Handle(
            new TutorAskFeatures.AskTutorQuestionCommand("Hello", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TutorRuntime.TutorRequired", result.Error.Code);
    }

    private static Setup CreateSetup()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeTutorRepository();
        var tutor = Tutor.Create("Robotics Tutor", null, userId);
        tutor.ActiveRevisionId = Guid.NewGuid();
        repository.Tutors[tutor.Id] = tutor;
        var runtime = new RecordingRuntime();
        var handler = new TutorAskFeatures.AskTutorQuestionHandler(
            runtime,
            repository,
            new FakeCurrentUserContext(userId));
        return new Setup(tutor, runtime, handler);
    }

    private sealed record Setup(
        Tutor Tutor,
        RecordingRuntime Runtime,
        TutorAskFeatures.AskTutorQuestionHandler Handler);

    private sealed class RecordingRuntime : ITutorRuntime
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
                Guid.NewGuid(),
                3,
                Guid.NewGuid(),
                "An encoder measures position or motion.",
                new TutorRuntimeModelMetadata(
                    Guid.NewGuid(),
                    "Llama 3.2",
                    "llama3.2:1b",
                    Guid.NewGuid(),
                    "Local Ollama"),
                null,
                false,
                [],
                new TutorRuntimeUsageMetadata(1),
                null)));
        }
    }
}
