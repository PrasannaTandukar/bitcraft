using System.Globalization;
using System.Text;
using System.Text.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>
/// D25 step 7 (plan.erm.md §7) — TutorRevision aggregate use-cases. A revision is the addressable
/// unit of tutor versioning; activation captures a <see cref="TutorConfigurationSnapshot"/> with the
/// materialized effective document set + knowledge binding.
/// </summary>
public static class TutorRevisionFeatures
{
    private static readonly Error NotFound = new("TutorRevision.NotFound", "Tutor revision not found.");
    private static readonly Error Forbidden = new("TutorRevision.Forbidden", "Editor or Owner membership required.");
    private static readonly Error TutorNotFound = new("Tutor.NotFound", "Tutor not found.");

    private sealed record ValidatedConfiguration(
        PromptVersion? PromptVersion,
        InteractionModel? InteractionModel,
        EmbeddingModel? QueryEmbeddingModel);

    // ---------- Create Draft ----------
    public sealed record CreateDraftRevisionCommand(Guid TutorId) : IRequest<Result<TutorRevision>>;

    public sealed class CreateDraftRevisionHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<CreateDraftRevisionCommand, Result<TutorRevision>>
    {
        public async Task<Result<TutorRevision>> Handle(CreateDraftRevisionCommand request, CancellationToken cancellationToken)
        {
            var tutor = await tutorRepository.GetByIdAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<TutorRevision>(TutorNotFound);
            }

            var role = await membership.GetRoleAsync(tutor.Id, currentUser.UserId, cancellationToken);
            if (role is not ResourceRole.Owner and not ResourceRole.Editor)
            {
                return Result.Failure<TutorRevision>(Forbidden);
            }

            var nextVersion = await tutorRepository.GetNextRevisionVersionNumberAsync(tutor.Id, cancellationToken);

            var revision = new TutorRevision
            {
                TutorId = tutor.Id,
                VersionNumber = nextVersion,
                Status = RevisionStatus.Draft,
            };
            revision.UpdateMetadata(currentUser.UserId);

            var saved = await tutorRepository.AddRevisionAsync(revision, cancellationToken);
            return Result.Success(saved);
        }
    }

    // ---------- Update Draft ----------
    public sealed record UpdateDraftRevisionCommand(
        Guid TutorId,
        Guid RevisionId,
        Guid? PromptVersionId,
        Guid? InteractionModelId,
        Guid? QueryEmbeddingModelId,
        string? InteractionParametersJson,
        DeploymentTarget DeploymentTarget,
        string? DeploymentConfigJson) : IRequest<Result<TutorRevision>>;

    public sealed class UpdateDraftRevisionHandler(
        ITutorRepository tutorRepository,
        IModelRepository modelRepository,
        IRuntimeEngineRepository runtimeEngines,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<UpdateDraftRevisionCommand, Result<TutorRevision>>
    {
        public async Task<Result<TutorRevision>> Handle(UpdateDraftRevisionCommand request, CancellationToken cancellationToken)
        {
            var revision = await tutorRepository.GetRevisionByIdAsync(request.RevisionId, cancellationToken);
            if (revision is null || revision.TutorId != request.TutorId)
            {
                return Result.Failure<TutorRevision>(NotFound);
            }
            if (revision.Status != RevisionStatus.Draft)
            {
                return Result.Failure<TutorRevision>(new Error("TutorRevision.NotDraft", "Only Draft revisions may be edited."));
            }

            var role = await membership.GetRoleAsync(revision.TutorId, currentUser.UserId, cancellationToken);
            if (role is not ResourceRole.Owner and not ResourceRole.Editor)
            {
                return Result.Failure<TutorRevision>(Forbidden);
            }

            var validation = await ValidateConfigurationAsync(
                tutorRepository,
                modelRepository,
                runtimeEngines,
                request.TutorId,
                request.PromptVersionId,
                request.InteractionModelId,
                request.QueryEmbeddingModelId,
                request.InteractionParametersJson,
                request.DeploymentTarget,
                request.DeploymentConfigJson,
                requirePublishable: false,
                cancellationToken);
            if (validation.IsFailure)
            {
                return Result.Failure<TutorRevision>(validation.Error);
            }

            revision.PromptVersionId = request.PromptVersionId;
            revision.PromptVersion = validation.Value.PromptVersion;
            revision.InteractionModelId = request.InteractionModelId;
            revision.InteractionModel = validation.Value.InteractionModel;
            revision.QueryEmbeddingModelId = request.QueryEmbeddingModelId;
            revision.QueryEmbeddingModel = validation.Value.QueryEmbeddingModel;
            revision.InteractionParametersJson = NormalizeOptionalJson(request.InteractionParametersJson);
            revision.DeploymentTarget = request.DeploymentTarget;
            revision.DeploymentConfigJson = NormalizeOptionalJson(request.DeploymentConfigJson);
            revision.UpdateMetadata(currentUser.UserId);

            await tutorRepository.UpdateRevisionAsync(revision, cancellationToken);
            return Result.Success(revision);
        }
    }

    // ---------- Publish ----------
    public sealed record PublishRevisionCommand(Guid TutorId, Guid RevisionId) : IRequest<Result<TutorRevision>>;

    public sealed class PublishRevisionHandler(
        ITutorRepository tutorRepository,
        IModelRepository modelRepository,
        IRuntimeEngineRepository runtimeEngines,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<PublishRevisionCommand, Result<TutorRevision>>
    {
        public async Task<Result<TutorRevision>> Handle(PublishRevisionCommand request, CancellationToken cancellationToken)
        {
            var revision = await tutorRepository.GetRevisionByIdAsync(request.RevisionId, cancellationToken);
            if (revision is null || revision.TutorId != request.TutorId)
            {
                return Result.Failure<TutorRevision>(NotFound);
            }

            var role = await membership.GetRoleAsync(revision.TutorId, currentUser.UserId, cancellationToken);
            if (role is not ResourceRole.Owner and not ResourceRole.Editor)
            {
                return Result.Failure<TutorRevision>(Forbidden);
            }

            var validation = await ValidateConfigurationAsync(
                tutorRepository,
                modelRepository,
                runtimeEngines,
                revision.TutorId,
                revision.PromptVersionId,
                revision.InteractionModelId,
                revision.QueryEmbeddingModelId,
                revision.InteractionParametersJson,
                revision.DeploymentTarget,
                revision.DeploymentConfigJson,
                requirePublishable: true,
                cancellationToken);
            if (validation.IsFailure)
            {
                return Result.Failure<TutorRevision>(validation.Error);
            }

            revision.PromptVersion = validation.Value.PromptVersion;
            revision.InteractionModel = validation.Value.InteractionModel;
            revision.QueryEmbeddingModel = validation.Value.QueryEmbeddingModel;

            try
            {
                revision.Publish(currentUser.UserId);
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure<TutorRevision>(new Error("TutorRevision.NotPublishable", ex.Message));
            }

            revision.UpdateMetadata(currentUser.UserId);
            await tutorRepository.UpdateRevisionAsync(revision, cancellationToken);
            return Result.Success(revision);
        }
    }

    // ---------- Activate ----------
    public sealed record ActivateRevisionCommand(Guid TutorId, Guid RevisionId) : IRequest<Result<TutorRevision>>;

    public sealed class ActivateRevisionHandler(
        ITutorRepository tutorRepository,
        IModelRepository modelRepository,
        IRuntimeEngineRepository runtimeEngines,
        IRevisionEffectiveSetReader effectiveSetReader,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<ActivateRevisionCommand, Result<TutorRevision>>
    {
        public async Task<Result<TutorRevision>> Handle(ActivateRevisionCommand request, CancellationToken cancellationToken)
        {
            var revision = await tutorRepository.GetRevisionByIdAsync(request.RevisionId, cancellationToken);
            if (revision is null || revision.TutorId != request.TutorId)
            {
                return Result.Failure<TutorRevision>(NotFound);
            }

            var role = await membership.GetRoleAsync(revision.TutorId, currentUser.UserId, cancellationToken);
            if (role is not ResourceRole.Owner and not ResourceRole.Editor)
            {
                return Result.Failure<TutorRevision>(Forbidden);
            }

            if (revision.Status != RevisionStatus.Published)
            {
                return Result.Failure<TutorRevision>(new Error(
                    "TutorRevision.NotPublished",
                    "Only Published revisions may be activated."));
            }

            var validation = await ValidateConfigurationAsync(
                tutorRepository,
                modelRepository,
                runtimeEngines,
                revision.TutorId,
                revision.PromptVersionId,
                revision.InteractionModelId,
                revision.QueryEmbeddingModelId,
                revision.InteractionParametersJson,
                revision.DeploymentTarget,
                revision.DeploymentConfigJson,
                requirePublishable: true,
                cancellationToken);
            if (validation.IsFailure)
            {
                return Result.Failure<TutorRevision>(validation.Error);
            }

            revision.PromptVersion = validation.Value.PromptVersion;
            revision.InteractionModel = validation.Value.InteractionModel;
            revision.QueryEmbeddingModel = validation.Value.QueryEmbeddingModel;

            var tutor = await tutorRepository.GetByIdWithDetailsAsync(revision.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<TutorRevision>(TutorNotFound);
            }

            // Mutate the complete transition in memory, then persist it through one atomic
            // repository SaveChanges call (per ERM §7.2).
            var prior = await tutorRepository.GetActiveRevisionAsync(tutor.Id, cancellationToken);
            string? previousSlug = null;
            if (prior is not null && prior.Id != revision.Id)
            {
                previousSlug = prior.ActivationSlug;
                prior.Archive(currentUser.UserId);
                prior.UpdateMetadata(currentUser.UserId);
            }

            var slug = await GenerateUniqueSlugAsync(tutorRepository, tutor.Name, revision.Id, cancellationToken);

            try
            {
                revision.Activate(currentUser.UserId, slug, previousSlug);
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure<TutorRevision>(new Error("TutorRevision.ActivateInvalid", ex.Message));
            }

            revision.UpdateMetadata(currentUser.UserId);

            // Capture snapshot bound to the newly activated revision.
            var effective = await effectiveSetReader.GetEffectiveSetAsync(
                tutor.Id,
                cancellationToken,
                revision.QueryEmbeddingModelId);
            var effectiveDocIdsJson = effective.DocumentIds.Count == 0
                ? null
                : JsonSerializer.Serialize(effective.DocumentIds);
            var effectiveBindingJson = effective.KnowledgeBindings.Count == 0
                ? null
                : JsonSerializer.Serialize(effective.KnowledgeBindings);

            var toolsJson = revision.RevisionTools.Count == 0
                ? null
                : JsonSerializer.Serialize(revision.RevisionTools.Select(rt => new
                {
                    rt.ToolDefinitionId,
                    rt.ToolDefinition?.Name,
                    rt.ToolDefinition?.Description,
                    rt.ToolDefinition?.JsonSchema,
                    rt.ToolDefinition?.ExecutionUrl
                }));

            var deploymentJson = JsonSerializer.Serialize(new
            {
                target = revision.DeploymentTarget.ToString(),
                configurationJson = revision.DeploymentConfigJson
            });

            var snapshot = TutorConfigurationSnapshot.Capture(
                tutor,
                revision.VersionNumber,
                revision.PromptVersion,
                tutor.TutorModelConnection,
                toolsJson,
                knowledgeSourcesJson: null,
                tutor.DeploymentProfile,
                currentUser.UserId,
                tutorRevision: revision,
                effectiveDocumentIdsJson: effectiveDocIdsJson,
                effectiveKnowledgeBindingJson: effectiveBindingJson,
                revisionDeploymentProfileJson: deploymentJson);
            revision.Snapshots.Add(snapshot);

            // Update Tutor.ActiveRevisionId pointer (plan.erm.md §7.1).
            tutor.ActiveRevisionId = revision.Id;
            tutor.ActiveRevision = revision;
            tutor.Status = TutorStatus.Active;
            await tutorRepository.ActivateRevisionAsync(tutor, revision, prior, snapshot, cancellationToken);

            return Result.Success(revision);
        }

        private static async Task<string> GenerateUniqueSlugAsync(
            ITutorRepository repo,
            string tutorName,
            Guid revisionId,
            CancellationToken ct)
        {
            var baseSlug = Slugify(tutorName);
            if (string.IsNullOrEmpty(baseSlug))
            {
                baseSlug = "tutor";
            }

            var candidate = baseSlug;
            var suffix = 1;
            while (await repo.ActivationSlugExistsAsync(candidate, revisionId, ct))
            {
                suffix++;
                candidate = $"{baseSlug}-{suffix}";
            }
            return candidate;
        }

        private static string Slugify(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var normalized = input.Normalize(NormalizationForm.FormKD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }
                if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
                {
                    sb.Append(ch);
                }
                else if (ch is >= 'A' and <= 'Z')
                {
                    sb.Append((char)(ch + 32));
                }
                else if (sb.Length > 0 && sb[^1] != '-')
                {
                    sb.Append('-');
                }
            }

            var slug = sb.ToString().Trim('-');
            return slug.Length > 80 ? slug[..80].TrimEnd('-') : slug;
        }
    }

    // ---------- Archive ----------
    public sealed record ArchiveRevisionCommand(Guid TutorId, Guid RevisionId) : IRequest<Result<TutorRevision>>;

    public sealed class ArchiveRevisionHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<ArchiveRevisionCommand, Result<TutorRevision>>
    {
        public async Task<Result<TutorRevision>> Handle(ArchiveRevisionCommand request, CancellationToken cancellationToken)
        {
            var revision = await tutorRepository.GetRevisionByIdAsync(request.RevisionId, cancellationToken);
            if (revision is null || revision.TutorId != request.TutorId)
            {
                return Result.Failure<TutorRevision>(NotFound);
            }

            var role = await membership.GetRoleAsync(revision.TutorId, currentUser.UserId, cancellationToken);
            if (role is not ResourceRole.Owner and not ResourceRole.Editor)
            {
                return Result.Failure<TutorRevision>(Forbidden);
            }

            try
            {
                revision.Archive(currentUser.UserId);
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure<TutorRevision>(new Error("TutorRevision.ArchiveInvalid", ex.Message));
            }

            revision.UpdateMetadata(currentUser.UserId);
            await tutorRepository.UpdateRevisionAsync(revision, cancellationToken);

            // If the archived revision was the tutor's active pointer, clear it.
            var tutor = await tutorRepository.GetByIdAsync(revision.TutorId, cancellationToken);
            if (tutor is not null && tutor.ActiveRevisionId == revision.Id)
            {
                tutor.ActiveRevisionId = null;
                tutor.ActiveRevision = null;
                tutor.Status = TutorStatus.Inactive;
                await tutorRepository.UpdateAsync(tutor, cancellationToken);
            }

            return Result.Success(revision);
        }
    }

    // ---------- Discard Draft ----------
    public sealed record DiscardDraftRevisionCommand(Guid TutorId, Guid RevisionId) : IRequest<Result<bool>>;

    public sealed class DiscardDraftRevisionHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<DiscardDraftRevisionCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(DiscardDraftRevisionCommand request, CancellationToken cancellationToken)
        {
            var revision = await tutorRepository.GetRevisionByIdAsync(request.RevisionId, cancellationToken);
            if (revision is null || revision.TutorId != request.TutorId)
            {
                return Result.Failure<bool>(NotFound);
            }
            if (revision.Status != RevisionStatus.Draft)
            {
                return Result.Failure<bool>(new Error("TutorRevision.NotDraft", "Only Draft revisions may be discarded."));
            }

            var role = await membership.GetRoleAsync(revision.TutorId, currentUser.UserId, cancellationToken);
            if (role is not ResourceRole.Owner and not ResourceRole.Editor)
            {
                return Result.Failure<bool>(Forbidden);
            }

            await tutorRepository.RemoveRevisionAsync(revision, cancellationToken);
            return Result.Success(true);
        }
    }

    // ---------- Queries ----------
    public sealed record GetRevisionByIdQuery(Guid TutorId, Guid RevisionId) : IRequest<Result<TutorRevision>>;

    public sealed class GetRevisionByIdHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<GetRevisionByIdQuery, Result<TutorRevision>>
    {
        public async Task<Result<TutorRevision>> Handle(GetRevisionByIdQuery request, CancellationToken cancellationToken)
        {
            var revision = await tutorRepository.GetRevisionByIdAsync(request.RevisionId, cancellationToken);
            if (revision is null || revision.TutorId != request.TutorId)
            {
                return Result.Failure<TutorRevision>(NotFound);
            }

            var role = await membership.GetRoleAsync(revision.TutorId, currentUser.UserId, cancellationToken);
            if (role is null)
            {
                return Result.Failure<TutorRevision>(Forbidden);
            }

            return Result.Success(revision);
        }
    }

    public sealed record ListRevisionsForTutorQuery(Guid TutorId) : IRequest<Result<IReadOnlyList<TutorRevision>>>;

    public sealed class ListRevisionsForTutorHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListRevisionsForTutorQuery, Result<IReadOnlyList<TutorRevision>>>
    {
        public async Task<Result<IReadOnlyList<TutorRevision>>> Handle(ListRevisionsForTutorQuery request, CancellationToken cancellationToken)
        {
            var role = await membership.GetRoleAsync(request.TutorId, currentUser.UserId, cancellationToken);
            if (role is null)
            {
                return Result.Failure<IReadOnlyList<TutorRevision>>(Forbidden);
            }

            var list = await tutorRepository.ListRevisionsByTutorAsync(request.TutorId, cancellationToken);
            return Result.Success<IReadOnlyList<TutorRevision>>(list);
        }
    }

    private static async Task<Result<ValidatedConfiguration>> ValidateConfigurationAsync(
        ITutorRepository tutorRepository,
        IModelRepository modelRepository,
        IRuntimeEngineRepository runtimeEngines,
        Guid tutorId,
        Guid? promptVersionId,
        Guid? interactionModelId,
        Guid? queryEmbeddingModelId,
        string? interactionParametersJson,
        DeploymentTarget deploymentTarget,
        string? deploymentConfigJson,
        bool requirePublishable,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(deploymentTarget))
        {
            return Result.Failure<ValidatedConfiguration>(new Error(
                "TutorRevision.InvalidDeploymentTarget",
                "Deployment target must be Local or Server."));
        }

        if (!TutorInteractionParametersParser.TryParse(interactionParametersJson, out _, out var parameterError))
        {
            return Result.Failure<ValidatedConfiguration>(new Error(
                "TutorRevision.InvalidInteractionParameters",
                parameterError.Description));
        }

        if (!IsValidOptionalJson(deploymentConfigJson))
        {
            return Result.Failure<ValidatedConfiguration>(new Error(
                "TutorRevision.InvalidDeploymentConfig",
                "Deployment configuration must be valid JSON."));
        }

        if (requirePublishable && promptVersionId is null)
        {
            return Result.Failure<ValidatedConfiguration>(new Error(
                "TutorRevision.NotPublishable",
                "This revision cannot be published because no prompt version is selected."));
        }

        if (requirePublishable && interactionModelId is null)
        {
            return Result.Failure<ValidatedConfiguration>(new Error(
                "TutorRevision.NotPublishable",
                "This revision cannot be published because no interaction model is selected."));
        }

        PromptVersion? prompt = null;
        if (promptVersionId.HasValue)
        {
            prompt = await tutorRepository.GetPromptVersionByIdAsync(promptVersionId.Value, cancellationToken);
            if (prompt is null)
            {
                return Result.Failure<ValidatedConfiguration>(new Error(
                    "PromptVersion.NotFound",
                    "The selected prompt version no longer exists."));
            }

            if (prompt.TutorId != tutorId)
            {
                return Result.Failure<ValidatedConfiguration>(new Error(
                    "PromptVersion.TutorMismatch",
                    "The selected prompt version does not belong to this tutor."));
            }
        }

        InteractionModel? interactionModel = null;
        if (interactionModelId.HasValue)
        {
            var model = await modelRepository.GetByIdAsync(interactionModelId.Value, cancellationToken);
            if (model is null)
            {
                return Result.Failure<ValidatedConfiguration>(new Error(
                    "InteractionModel.NotFound",
                    "The selected interaction model no longer exists."));
            }

            if (model is not InteractionModel selectedInteractionModel)
            {
                return Result.Failure<ValidatedConfiguration>(new Error(
                    "InteractionModel.InvalidType",
                    "The selected model is not an interaction model."));
            }

            if (!selectedInteractionModel.IsEnabled)
            {
                return Result.Failure<ValidatedConfiguration>(new Error(
                    "InteractionModel.Disabled",
                    "The selected interaction model is disabled."));
            }

            var engine = await runtimeEngines.GetByIdAsync(selectedInteractionModel.RuntimeEngineId, cancellationToken);
            if (engine is null)
            {
                return Result.Failure<ValidatedConfiguration>(new Error(
                    "RuntimeEngine.NotFound",
                    "The interaction model's runtime engine no longer exists."));
            }

            if (!engine.IsEnabled)
            {
                return Result.Failure<ValidatedConfiguration>(new Error(
                    "RuntimeEngine.Disabled",
                    "The interaction model's runtime engine is disabled."));
            }

            selectedInteractionModel.RuntimeEngine = engine;
            interactionModel = selectedInteractionModel;
        }

        EmbeddingModel? embeddingModel = null;
        if (queryEmbeddingModelId.HasValue)
        {
            var model = await modelRepository.GetByIdAsync(queryEmbeddingModelId.Value, cancellationToken);
            if (model is null)
            {
                return Result.Failure<ValidatedConfiguration>(new Error(
                    "EmbeddingModel.NotFound",
                    "The selected embedding model no longer exists."));
            }

            if (model is not EmbeddingModel selectedEmbeddingModel)
            {
                return Result.Failure<ValidatedConfiguration>(new Error(
                    "EmbeddingModel.InvalidType",
                    "The selected model is not an embedding model."));
            }

            if (!selectedEmbeddingModel.IsEnabled || selectedEmbeddingModel.EmbeddingDimensions <= 0)
            {
                return Result.Failure<ValidatedConfiguration>(new Error(
                    "EmbeddingModel.Disabled",
                    "The selected embedding model is disabled or invalid."));
            }

            var engine = await runtimeEngines.GetByIdAsync(selectedEmbeddingModel.RuntimeEngineId, cancellationToken);
            if (engine is null)
            {
                return Result.Failure<ValidatedConfiguration>(new Error(
                    "RuntimeEngine.NotFound",
                    "The embedding model's runtime engine no longer exists."));
            }

            if (!engine.IsEnabled)
            {
                return Result.Failure<ValidatedConfiguration>(new Error(
                    "RuntimeEngine.Disabled",
                    "The embedding model's runtime engine is disabled."));
            }

            selectedEmbeddingModel.RuntimeEngine = engine;
            embeddingModel = selectedEmbeddingModel;
        }

        return Result.Success(new ValidatedConfiguration(prompt, interactionModel, embeddingModel));
    }

    private static bool IsValidOptionalJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? NormalizeOptionalJson(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
