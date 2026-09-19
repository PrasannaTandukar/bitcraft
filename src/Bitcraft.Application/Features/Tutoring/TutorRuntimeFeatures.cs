using System.Text.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

public static class TutorRuntimeFeatures
{
    public sealed record AskTutorRuntimeCommand(
        string ActivationSlug,
        string Question,
        int? TopK,
        IReadOnlyList<TutorRuntimeConversationMessage>? History,
        IReadOnlyList<Guid>? AttachmentIds = null)
        : IRequest<Result<TutorRuntimeResult>>;

    public sealed class AskTutorRuntimeHandler(
        ITutorRepository tutors,
        ITutorRuntime runtime,
        ICurrentUserContext currentUser)
        : IRequestHandler<AskTutorRuntimeCommand, Result<TutorRuntimeResult>>
    {
        public async Task<Result<TutorRuntimeResult>> Handle(
            AskTutorRuntimeCommand request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ActivationSlug))
            {
                return Result.Failure<TutorRuntimeResult>(new Error(
                    "TutorRuntime.TutorNotFound",
                    "Tutor was not found."));
            }

            var revision = await tutors.GetActiveRevisionBySlugAsync(
                request.ActivationSlug.Trim(),
                cancellationToken);
            if (revision is null)
            {
                return Result.Failure<TutorRuntimeResult>(new Error(
                    "TutorRuntime.TutorNotFound",
                    "Tutor was not found."));
            }

            return await runtime.ExecuteAsync(
                new TutorRuntimeRequest(
                    revision.TutorId,
                    null,
                    null,
                    request.Question,
                    currentUser.UserId,
                    true,
                    request.TopK ?? TutorRuntime.DefaultTopK,
                    TutorRuntimeExecutionMode.Interactive,
                    request.History,
                    request.AttachmentIds),
                cancellationToken);
        }
    }

    public sealed record ListRuntimeTutorsQuery : IRequest<IReadOnlyList<TutorRuntimeDescriptor>>;

    public sealed class ListRuntimeTutorsHandler(
        ITutorRepository tutors,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListRuntimeTutorsQuery, IReadOnlyList<TutorRuntimeDescriptor>>
    {
        public async Task<IReadOnlyList<TutorRuntimeDescriptor>> Handle(
            ListRuntimeTutorsQuery request,
            CancellationToken cancellationToken)
        {
            var visible = await tutors.ListByOwnerAsync(currentUser.UserId, cancellationToken);
            return visible
                .Select(ToDescriptor)
                .Where(value => value is not null)
                .Cast<TutorRuntimeDescriptor>()
                .OrderBy(value => value.TutorName)
                .ToList();
        }

        private static TutorRuntimeDescriptor? ToDescriptor(Tutor tutor)
        {
            var revision = tutor.ActiveRevision;
            if (tutor.Status != TutorStatus.Active ||
                revision is null ||
                tutor.ActiveRevisionId != revision.Id ||
                revision.Status != RevisionStatus.Active ||
                string.IsNullOrWhiteSpace(revision.ActivationSlug) ||
                revision.PromptVersionId is null ||
                revision.InteractionModel is null ||
                !revision.InteractionModel.IsEnabled ||
                revision.InteractionModel.RuntimeEngine?.IsEnabled != true ||
                revision.InteractionModel.RuntimeEngine.ProviderType.ToLlmDialect() == LlmDialect.Unsupported ||
                string.IsNullOrWhiteSpace(revision.InteractionModel.ProviderModelName))
            {
                return null;
            }

            var snapshot = revision.Snapshots
                .Where(value => value.TutorRevisionId == revision.Id)
                .OrderByDescending(value => value.CapturedUtc)
                .FirstOrDefault();
            if (snapshot is null ||
                snapshot.PromptVersionId != revision.PromptVersionId ||
                snapshot.InteractionModelId != revision.InteractionModelId ||
                string.IsNullOrWhiteSpace(snapshot.PromptContent) ||
                string.IsNullOrWhiteSpace(snapshot.ModelName) ||
                !TryReadDocumentIds(snapshot.EffectiveDocumentIdsJson, out var documentIds))
            {
                return null;
            }

            var knowledgeCount = documentIds.Count;
            var embedding = revision.QueryEmbeddingModel;
            if (knowledgeCount > 0 &&
                (embedding is null ||
                 !embedding.IsEnabled ||
                 embedding.EmbeddingDimensions <= 0 ||
                 embedding.RuntimeEngine?.IsEnabled != true ||
                 embedding.RuntimeEngine.ProviderType.ToEmbeddingDialect() == LlmDialect.Unsupported ||
                 string.IsNullOrWhiteSpace(embedding.ProviderModelName) ||
                 !TryReadKnowledgeBindings(snapshot.EffectiveKnowledgeBindingJson, out var bindings) ||
                 documentIds.Any(documentId => !bindings.Any(binding =>
                     binding.DocumentId == documentId &&
                     binding.EmbeddingModelId == embedding.Id &&
                     binding.Dimensions == embedding.EmbeddingDimensions))))
            {
                return null;
            }

            var interactionEngine = revision.InteractionModel.RuntimeEngine!;
            return new TutorRuntimeDescriptor(
                tutor.Id,
                tutor.Name,
                tutor.Description,
                revision.Id,
                revision.VersionNumber,
                revision.ActivationSlug,
                snapshot.Id,
                new TutorRuntimeModelMetadata(
                    revision.InteractionModel.Id,
                    revision.InteractionModel.Name,
                    snapshot.ModelName,
                    interactionEngine.Id,
                    interactionEngine.Name,
                    snapshot.ModelCapabilities.HasFlag(ModelCapabilities.ImageInput)),
                embedding?.RuntimeEngine is null
                    ? null
                    : new TutorRuntimeEmbeddingModelMetadata(
                        embedding.Id,
                        embedding.Name,
                        embedding.ProviderModelName,
                        embedding.EmbeddingDimensions,
                        embedding.RuntimeEngine.Id,
                        embedding.RuntimeEngine.Name),
                knowledgeCount);
        }

        private static bool TryReadDocumentIds(string? json, out IReadOnlyList<Guid> documentIds)
        {
            documentIds = [];
            if (string.IsNullOrWhiteSpace(json))
            {
                return true;
            }

            try
            {
                var ids = JsonSerializer.Deserialize<List<Guid>>(json);
                if (ids is null || ids.Any(value => value == Guid.Empty))
                {
                    return false;
                }
                documentIds = ids.Distinct().ToList();
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool TryReadKnowledgeBindings(
            string? json,
            out IReadOnlyList<EffectiveKnowledgeBinding> bindings)
        {
            bindings = [];
            if (string.IsNullOrWhiteSpace(json))
            {
                return true;
            }

            try
            {
                bindings = JsonSerializer.Deserialize<List<EffectiveKnowledgeBinding>>(json) ?? [];
                return bindings.All(binding =>
                    binding.DocumentId != Guid.Empty &&
                    binding.EmbeddingModelId != Guid.Empty &&
                    binding.Dimensions > 0);
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
