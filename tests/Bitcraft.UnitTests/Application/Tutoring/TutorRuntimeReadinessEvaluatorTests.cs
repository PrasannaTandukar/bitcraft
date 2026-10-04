using System.Text.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Features.Tutoring;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Xunit;

namespace Bitcraft.UnitTests.Application.Tutoring;

public sealed class TutorRuntimeReadinessEvaluatorTests
{
    [Fact]
    public void ActiveConfigurationWithoutKnowledgeIsChatReadyWithoutEmbeddingModel()
    {
        var tutor = CreateTutor();
        var result = TutorRuntimeReadinessEvaluator.Evaluate(tutor).Readiness;

        Assert.True(result.IsChatReady);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void KnowledgeWithMatchingEmbeddingModelAndDimensionsIsChatReady()
    {
        var tutor = CreateTutor(knowledge: true);

        var result = TutorRuntimeReadinessEvaluator.Evaluate(tutor).Readiness;

        Assert.True(result.IsChatReady);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void KnowledgeWithoutQueryEmbeddingModelHasActionableReason()
    {
        var tutor = CreateTutor(knowledge: true, configureEmbedding: false);

        var result = TutorRuntimeReadinessEvaluator.Evaluate(tutor).Readiness;

        Assert.False(result.IsChatReady);
        Assert.Contains(result.Reasons, reason => reason.Code == "MissingEmbeddingModel" && reason.Message.Contains("new revision", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DisabledInteractionModelIsNotReady()
    {
        var tutor = CreateTutor();
        tutor.ActiveRevision!.InteractionModel!.IsEnabled = false;

        var result = TutorRuntimeReadinessEvaluator.Evaluate(tutor).Readiness;

        Assert.False(result.IsChatReady);
        Assert.Contains(result.Reasons, reason => reason.Code == "InteractionModelDisabled");
    }

    [Fact]
    public void DisabledInteractionProviderIsNotReady()
    {
        var tutor = CreateTutor();
        tutor.ActiveRevision!.InteractionModel!.RuntimeEngine!.IsEnabled = false;

        var result = TutorRuntimeReadinessEvaluator.Evaluate(tutor).Readiness;

        Assert.False(result.IsChatReady);
        Assert.Contains(result.Reasons, reason => reason.Code == "ProviderDisabled");
    }

    [Fact]
    public void MissingSnapshotIsNotReady()
    {
        var tutor = CreateTutor(includeSnapshot: false);

        var result = TutorRuntimeReadinessEvaluator.Evaluate(tutor).Readiness;

        Assert.False(result.IsChatReady);
        Assert.Contains(result.Reasons, reason => reason.Code == "MissingSnapshot");
    }

    [Fact]
    public void CorruptSnapshotKnowledgeMetadataIsNotReady()
    {
        var tutor = CreateTutor(knowledge: true, corruptSnapshot: true);

        var result = TutorRuntimeReadinessEvaluator.Evaluate(tutor).Readiness;

        Assert.False(result.IsChatReady);
        Assert.Contains(result.Reasons, reason => reason.Code == "SnapshotInvalid");
    }

    [Fact]
    public void DifferentDocumentEmbeddingModelIsNotReady()
    {
        var tutor = CreateTutor(knowledge: true, bindingModelMismatch: true);

        var result = TutorRuntimeReadinessEvaluator.Evaluate(tutor).Readiness;

        Assert.False(result.IsChatReady);
        Assert.Contains(result.Reasons, reason => reason.Code == "KnowledgeEmbeddingModelMismatch");
    }

    [Fact]
    public void DifferentDocumentEmbeddingDimensionsAreNotReady()
    {
        var tutor = CreateTutor(knowledge: true, bindingDimensionsMismatch: true);

        var result = TutorRuntimeReadinessEvaluator.Evaluate(tutor).Readiness;

        Assert.False(result.IsChatReady);
        Assert.Contains(result.Reasons, reason => reason.Code == "KnowledgeEmbeddingDimensionMismatch");
    }

    [Fact]
    public void UnvectorizedDocumentIsNotReady()
    {
        var tutor = CreateTutor(knowledge: true, missingBinding: true);

        var result = TutorRuntimeReadinessEvaluator.Evaluate(tutor).Readiness;

        Assert.False(result.IsChatReady);
        Assert.Contains(result.Reasons, reason => reason.Code == "KnowledgeNotVectorized");
    }

    [Fact]
    public void CapturedSnapshotRemainsBoundToItsPublishedRevisionWhenNewRevisionIsConfigured()
    {
        var tutor = CreateTutor(knowledge: true, configureEmbedding: false);
        var originalRevision = tutor.ActiveRevision!;
        var originalSnapshot = originalRevision.Snapshots.Single();
        var originalSnapshotId = originalSnapshot.Id;
        var originalPromptId = originalSnapshot.PromptVersionId;

        var nextRevision = CreateTutor(knowledge: true).ActiveRevision!;
        nextRevision.TutorId = tutor.Id;
        nextRevision.Tutor = tutor;
        nextRevision.VersionNumber = originalRevision.VersionNumber + 1;
        var nextSnapshot = TutorConfigurationSnapshot.Capture(
            tutor,
            nextRevision.VersionNumber,
            nextRevision.PromptVersion,
            null,
            null,
            null,
            null,
            Guid.NewGuid(),
            nextRevision,
            "[]",
            "[]");
        nextRevision.Snapshots.Clear();
        nextRevision.Snapshots.Add(nextSnapshot);
        tutor.Revisions.Add(nextRevision);
        tutor.ActiveRevision = nextRevision;
        tutor.ActiveRevisionId = nextRevision.Id;

        var result = TutorRuntimeReadinessEvaluator.Evaluate(tutor).Readiness;

        Assert.True(result.IsChatReady);
        Assert.Equal(originalSnapshotId, originalSnapshot.Id);
        Assert.Equal(originalRevision.Id, originalSnapshot.TutorRevisionId);
        Assert.Equal(originalPromptId, originalSnapshot.PromptVersionId);
    }

    private static Tutor CreateTutor(
        bool knowledge = false,
        bool configureEmbedding = true,
        bool includeSnapshot = true,
        bool bindingModelMismatch = false,
        bool bindingDimensionsMismatch = false,
        bool missingBinding = false,
        bool corruptSnapshot = false)
    {
        var engine = new RuntimeEngine { Name = "Local runtime", ProviderType = RuntimeEngineProviderType.Ollama, Endpoint = "http://localhost", IsEnabled = true };
        var interaction = new InteractionModel { Name = "Chat model", RuntimeEngineId = engine.Id, RuntimeEngine = engine, ProviderModelName = "qwen3.8:27b", IsEnabled = true };
        var embedding = new EmbeddingModel { Name = "Embedding model", RuntimeEngineId = engine.Id, RuntimeEngine = engine, ProviderModelName = "nomic-embed-text:latest", EmbeddingDimensions = 768, IsEnabled = true };
        var tutor = Tutor.Create("RoboTutor", null, Guid.NewGuid());
        tutor.Status = TutorStatus.Active;
        var prompt = new PromptVersion { TutorId = tutor.Id, Name = "Prompt v1", VersionNumber = 1, Content = "Teach robotics." };
        var revision = new TutorRevision
        {
            TutorId = tutor.Id,
            Tutor = tutor,
            VersionNumber = 1,
            Status = RevisionStatus.Active,
            ActivationSlug = "robotutor",
            PromptVersionId = prompt.Id,
            PromptVersion = prompt,
            InteractionModelId = interaction.Id,
            InteractionModel = interaction,
            QueryEmbeddingModelId = knowledge && configureEmbedding ? embedding.Id : null,
            QueryEmbeddingModel = knowledge && configureEmbedding ? embedding : null
        };
        var documentIds = knowledge ? new[] { Guid.NewGuid() } : [];
        var bindings = !knowledge || missingBinding
            ? Array.Empty<EffectiveKnowledgeBinding>()
            : new[]
            {
                new EffectiveKnowledgeBinding(
                    documentIds[0],
                    bindingModelMismatch ? Guid.NewGuid() : embedding.Id,
                    bindingDimensionsMismatch ? 384 : embedding.EmbeddingDimensions)
            };

        if (includeSnapshot)
        {
            var snapshot = TutorConfigurationSnapshot.Capture(
                tutor,
                revision.VersionNumber,
                prompt,
                null,
                null,
                null,
                null,
                Guid.NewGuid(),
                revision,
                corruptSnapshot ? "not-json" : JsonSerializer.Serialize(documentIds),
                JsonSerializer.Serialize(bindings));
            tutor.Snapshots.Add(snapshot);
            revision.Snapshots.Add(snapshot);
        }
        tutor.ActiveRevisionId = revision.Id;
        tutor.ActiveRevision = revision;
        tutor.Revisions.Add(revision);
        return tutor;
    }
}
