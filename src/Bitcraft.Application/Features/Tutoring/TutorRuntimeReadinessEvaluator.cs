using System.Text.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>Shared runtime eligibility rules used by both the chat list and diagnostic surfaces.</summary>
public static class TutorRuntimeReadinessEvaluator
{
    private static readonly HashSet<RuntimeEngineProviderType> InteractionProviders =
    [
        RuntimeEngineProviderType.Ollama,
        RuntimeEngineProviderType.OpenAI,
        RuntimeEngineProviderType.OpenAICompatible,
        RuntimeEngineProviderType.vLLM,
        RuntimeEngineProviderType.LlamaCpp,
        RuntimeEngineProviderType.Anthropic
    ];

    private static readonly HashSet<RuntimeEngineProviderType> EmbeddingProviders =
    [
        RuntimeEngineProviderType.Ollama,
        RuntimeEngineProviderType.OpenAI,
        RuntimeEngineProviderType.OpenAICompatible,
        RuntimeEngineProviderType.LlamaCpp
    ];

    public static (TutorRuntimeReadinessResult Readiness, TutorRuntimeDescriptor? Descriptor) Evaluate(Tutor tutor)
    {
        var reasons = new List<TutorRuntimeReadinessReason>();
        var revision = tutor.ActiveRevision;
        TutorRuntimeDescriptor? descriptor = null;

        if (tutor.Status != TutorStatus.Active)
            Add("TutorInactive", "Activate the tutor before using it in chat.");
        if (revision is null || tutor.ActiveRevisionId != revision.Id)
            Add("NoActiveRevision", "Activate a revision to make it available for chat.");
        if (revision is not null)
        {
            if (revision.Status != RevisionStatus.Active)
                Add("RevisionNotActive", "The tutor's current revision is not active.");
            if (string.IsNullOrWhiteSpace(revision.ActivationSlug))
                Add("MissingActivationSlug", "The active revision is missing its chat address.");
            if (revision.PromptVersionId is null || revision.PromptVersion is null || string.IsNullOrWhiteSpace(revision.PromptVersion.Content))
                Add("MissingPrompt", "Add a prompt to a new draft revision, then publish and activate it.");

            var interaction = revision.InteractionModel;
            if (revision.InteractionModelId is null || interaction is null)
                Add("MissingInteractionModel", "Select an interaction model in a draft revision, then publish and activate it.");
            else
            {
                if (!interaction.IsEnabled)
                    Add("InteractionModelDisabled", $"{interaction.ProviderModelName} is disabled. Enable it or select an available model in a new revision.");
                var engine = interaction.RuntimeEngine;
                if (engine is null || !engine.IsEnabled)
                    Add("ProviderDisabled", $"The runtime for {interaction.ProviderModelName} is disabled. Enable it or select an available provider in a new revision.");
                else if (!InteractionProviders.Contains(engine.ProviderType))
                    Add("UnsupportedProvider", "The configured interaction provider is not supported for chat.");
                if (string.IsNullOrWhiteSpace(interaction.ProviderModelName))
                    Add("MissingProviderModelName", "Select an interaction model with a provider model name.");
            }

            var snapshot = revision.Snapshots
                .Where(value => value.TutorRevisionId == revision.Id)
                .OrderByDescending(value => value.CapturedUtc)
                .FirstOrDefault();
            IReadOnlyList<Guid> documentIds = [];
            IReadOnlyList<EffectiveKnowledgeBinding> bindings = [];
            if (snapshot is null)
                Add("MissingSnapshot", "Activate a published revision to capture its runtime configuration snapshot.");
            else
            {
                if (snapshot.TutorId != tutor.Id || snapshot.TutorRevisionId != revision.Id || snapshot.VersionNumber != revision.VersionNumber)
                    Add("SnapshotRevisionMismatch", "The configuration snapshot does not belong to the active revision.");
                if (snapshot.PromptVersionId != revision.PromptVersionId || string.IsNullOrWhiteSpace(snapshot.PromptContent))
                    Add("SnapshotPromptMismatch", "The configuration snapshot does not contain the active revision's prompt.");
                if (snapshot.InteractionModelId != revision.InteractionModelId || string.IsNullOrWhiteSpace(snapshot.ModelName))
                    Add("SnapshotModelMismatch", "The configuration snapshot does not contain the active revision's interaction model.");
                if (!TryReadDocumentIds(snapshot.EffectiveDocumentIdsJson, out documentIds) ||
                    !TryReadKnowledgeBindings(snapshot.EffectiveKnowledgeBindingJson, out bindings))
                    Add("SnapshotInvalid", "The configuration snapshot contains invalid knowledge metadata.");
            }

            if (snapshot is not null && !reasons.Any(reason => reason.Code == "SnapshotInvalid"))
            {
                var embedding = revision.QueryEmbeddingModel;
                if (documentIds.Count > 0)
                {
                    if (revision.QueryEmbeddingModelId is null || embedding is null)
                        Add("MissingEmbeddingModel", $"Knowledge is attached, but Revision {revision.VersionNumber} does not have a query embedding model. Create a new revision to add one.");
                    else
                    {
                        if (!embedding.IsEnabled)
                            Add("EmbeddingModelDisabled", "The query embedding model is disabled. Enable it or select an available model in a new revision.");
                        var embeddingEngine = embedding.RuntimeEngine;
                        if (embeddingEngine is null || !embeddingEngine.IsEnabled)
                            Add("EmbeddingRuntimeDisabled", "The query embedding runtime is disabled. Enable it or select an available provider in a new revision.");
                        else if (!EmbeddingProviders.Contains(embeddingEngine.ProviderType))
                            Add("UnsupportedEmbeddingProvider", "The configured query embedding provider is not supported for knowledge search.");
                        if (embedding.EmbeddingDimensions <= 0)
                            Add("InvalidEmbeddingDimensions", "The query embedding model must have a valid dimension count.");
                        if (string.IsNullOrWhiteSpace(embedding.ProviderModelName))
                            Add("MissingEmbeddingProviderModelName", "The query embedding model is missing its provider model name.");

                        var unvectorizedDocumentCount = 0;
                        foreach (var documentId in documentIds)
                        {
                            var documentBindings = bindings.Where(binding => binding.DocumentId == documentId).ToList();
                            if (documentBindings.Count == 0)
                            {
                                unvectorizedDocumentCount++;
                                continue;
                            }
                            if (documentBindings.Any(binding => binding.EmbeddingModelId != embedding.Id))
                                Add("KnowledgeEmbeddingModelMismatch", "One or more attached documents were vectorized with a different embedding model. Re-vectorize them with the revision's query model.");
                            if (documentBindings.Any(binding => binding.Dimensions != embedding.EmbeddingDimensions))
                                Add("KnowledgeEmbeddingDimensionMismatch", "One or more attached documents use embedding dimensions that do not match the query model.");
                        }
                        if (unvectorizedDocumentCount > 0)
                            Add("KnowledgeNotVectorized", $"{unvectorizedDocumentCount} attached document{(unvectorizedDocumentCount == 1 ? "" : "s")} still require vectorization.");
                    }
                }

                if (reasons.Count == 0 && revision.InteractionModel?.RuntimeEngine is { } engine && revision.InteractionModel is { } model && snapshot is not null)
                {
                    descriptor = new TutorRuntimeDescriptor(
                        tutor.Id,
                        tutor.Name,
                        tutor.Description,
                        revision.Id,
                        revision.VersionNumber,
                        revision.ActivationSlug!,
                        snapshot.Id,
                        new TutorRuntimeModelMetadata(
                            model.Id,
                            model.Name,
                            snapshot.ModelName!,
                            engine.Id,
                            engine.Name,
                            snapshot.ModelCapabilities.HasFlag(ModelCapabilities.ImageInput),
                            snapshot.ModelCapabilities.HasFlag(ModelCapabilities.FunctionCalling),
                            snapshot.ModelCapabilities.HasFlag(ModelCapabilities.StructuredOutput)),
                        revision.QueryEmbeddingModel is { RuntimeEngine: { } embeddingRuntime } readyEmbedding
                            ? new TutorRuntimeEmbeddingModelMetadata(readyEmbedding.Id, readyEmbedding.Name, readyEmbedding.ProviderModelName, readyEmbedding.EmbeddingDimensions, embeddingRuntime.Id, embeddingRuntime.Name)
                            : null,
                        documentIds.Count);
                }
            }
        }

        return (new TutorRuntimeReadinessResult(
            tutor.Id,
            tutor.Name,
            revision?.Id,
            revision?.VersionNumber,
            reasons.Count == 0 && descriptor is not null,
            reasons), descriptor);

        void Add(string code, string message)
        {
            if (reasons.All(reason => reason.Code != code))
                reasons.Add(new TutorRuntimeReadinessReason(code, message));
        }
    }

    private static bool TryReadDocumentIds(string? json, out IReadOnlyList<Guid> documentIds)
    {
        documentIds = [];
        if (string.IsNullOrWhiteSpace(json)) return true;
        try
        {
            var ids = JsonSerializer.Deserialize<List<Guid>>(json);
            if (ids is null || ids.Any(id => id == Guid.Empty)) return false;
            documentIds = ids.Distinct().ToList();
            return true;
        }
        catch (JsonException) { return false; }
    }

    private static bool TryReadKnowledgeBindings(string? json, out IReadOnlyList<EffectiveKnowledgeBinding> bindings)
    {
        bindings = [];
        if (string.IsNullOrWhiteSpace(json)) return true;
        try
        {
            bindings = JsonSerializer.Deserialize<List<EffectiveKnowledgeBinding>>(json) ?? [];
            return bindings.All(binding => binding.DocumentId != Guid.Empty && binding.EmbeddingModelId != Guid.Empty && binding.Dimensions > 0);
        }
        catch (JsonException) { return false; }
    }
}
