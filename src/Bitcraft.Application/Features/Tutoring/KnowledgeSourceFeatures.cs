using System.Text.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>
/// Canonical tutor knowledge attachment use-cases. TutorDocument and TutorDocumentFolder are
/// authoritative for new writes. KnowledgeSourceLink is retained only as an explicit compatibility read.
/// </summary>
public static class KnowledgeSourceFeatures
{
    public sealed record TutorKnowledgeDocumentResult(
        Guid LinkId,
        Guid TutorId,
        Guid DocumentId,
        string DocumentName,
        AttachmentSource AttachedVia,
        bool IsVectorized,
        Guid? EmbeddingModelId,
        string? EmbeddingModelName,
        bool LegacyCompatibility);

    public sealed record TutorKnowledgeFolderResult(
        Guid LinkId,
        Guid TutorId,
        Guid FolderId,
        string FolderName);

    public sealed record TutorKnowledgeEffectiveDocumentResult(
        Guid DocumentId,
        string DocumentName,
        string AttachedThrough,
        string ReadinessStatus,
        bool IsVectorized,
        bool IsStale,
        int ChunkCount,
        Guid? EmbeddingModelId,
        string? EmbeddingModelName,
        string? ProviderModelName,
        int? EmbeddingDimensions,
        bool ReadyForSnapshot,
        string? Reason);

    public sealed record TutorKnowledgeOverviewResult(
        IReadOnlyList<TutorKnowledgeDocumentResult> DirectDocuments,
        IReadOnlyList<TutorKnowledgeFolderResult> Folders,
        IReadOnlyList<TutorKnowledgeEffectiveDocumentResult> EffectiveDocuments,
        int EffectiveDocumentCount,
        int? ActiveRevisionNumber,
        Guid? ActiveSnapshotId,
        bool ActiveSnapshotMatchesCurrentKnowledge,
        Guid? QueryEmbeddingModelId,
        string? QueryEmbeddingModelName,
        string? QueryProviderModelName,
        int? QueryEmbeddingDimensions);

    public sealed record AttachTutorDocumentCommand(Guid TutorId, Guid DocumentId)
        : IRequest<Result<TutorDocument>>;

    // Legacy command name remains source-compatible, but it writes TutorDocument.
    public sealed record LinkKnowledgeSourceCommand(Guid TutorId, Guid DocumentId)
        : IRequest<Result<TutorDocument>>;

    public sealed class LinkKnowledgeSourceHandler(
        ITutorRepository tutorRepository,
        IDocumentStore documentStore,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<LinkKnowledgeSourceCommand, Result<TutorDocument>>,
          IRequestHandler<AttachTutorDocumentCommand, Result<TutorDocument>>
    {
        public Task<Result<TutorDocument>> Handle(
            LinkKnowledgeSourceCommand request,
            CancellationToken cancellationToken) =>
            AttachAsync(request.TutorId, request.DocumentId, cancellationToken);

        public Task<Result<TutorDocument>> Handle(
            AttachTutorDocumentCommand request,
            CancellationToken cancellationToken) =>
            AttachAsync(request.TutorId, request.DocumentId, cancellationToken);

        private async Task<Result<TutorDocument>> AttachAsync(
            Guid tutorId,
            Guid documentId,
            CancellationToken cancellationToken)
        {
            var authorization = await AuthorizeMutationAsync(
                tutorRepository,
                membership,
                currentUser,
                tutorId,
                cancellationToken);
            if (authorization.IsFailure)
            {
                return Result.Failure<TutorDocument>(authorization.Error);
            }

            var document = await documentStore.GetByIdAsync(documentId, cancellationToken);
            if (document is null)
            {
                return Result.Failure<TutorDocument>(new Error("Document.NotFound", "Document not found."));
            }
            if (document.CreatedById != currentUser.UserId)
            {
                return Result.Failure<TutorDocument>(new Error("Document.Forbidden", "Requesting user does not own this document."));
            }

            if (await tutorRepository.GetTutorDocumentAsync(tutorId, documentId, cancellationToken) is not null ||
                await tutorRepository.KnowledgeSourceLinkExistsAsync(tutorId, documentId, cancellationToken))
            {
                return Result.Failure<TutorDocument>(new Error(
                    "TutorKnowledge.AlreadyAttached",
                    "Document is already attached to this tutor."));
            }

            var link = new TutorDocument
            {
                TutorId = tutorId,
                DocumentId = documentId,
                AttachedVia = AttachmentSource.Direct,
                Document = document
            };
            return Result.Success(await tutorRepository.AddTutorDocumentAsync(link, cancellationToken));
        }
    }

    public sealed record ListTutorDocumentsQuery(Guid TutorId)
        : IRequest<Result<IReadOnlyList<TutorKnowledgeDocumentResult>>>;

    public sealed class ListTutorDocumentsHandler(
        ITutorRepository tutorRepository,
        IDocumentVectorizationReader vectorization,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListTutorDocumentsQuery, Result<IReadOnlyList<TutorKnowledgeDocumentResult>>>
    {
        public async Task<Result<IReadOnlyList<TutorKnowledgeDocumentResult>>> Handle(
            ListTutorDocumentsQuery request,
            CancellationToken cancellationToken)
        {
            var authorization = await AuthorizeReadAsync(
                tutorRepository,
                membership,
                currentUser,
                request.TutorId,
                cancellationToken);
            if (authorization.IsFailure)
            {
                return Result.Failure<IReadOnlyList<TutorKnowledgeDocumentResult>>(authorization.Error);
            }

            var canonical = await tutorRepository.ListTutorDocumentsAsync(request.TutorId, cancellationToken);
            var legacy = await tutorRepository.ListKnowledgeSourceLinksAsync(request.TutorId, cancellationToken);
            var results = new List<TutorKnowledgeDocumentResult>();
            foreach (var link in canonical)
            {
                var metadata = await vectorization.GetMetadataAsync(link.DocumentId, cancellationToken);
                results.Add(new TutorKnowledgeDocumentResult(
                    link.Id,
                    link.TutorId,
                    link.DocumentId,
                    link.Document?.Name ?? "Document",
                    link.AttachedVia,
                    metadata.IsVectorized && !metadata.IsStale,
                    metadata.EmbeddingModelId,
                    metadata.EmbeddingModelName,
                    LegacyCompatibility: false));
            }

            foreach (var link in legacy.Where(legacyLink =>
                         canonical.All(canonicalLink => canonicalLink.DocumentId != legacyLink.DocumentId)))
            {
                var metadata = await vectorization.GetMetadataAsync(link.DocumentId, cancellationToken);
                results.Add(new TutorKnowledgeDocumentResult(
                    link.Id,
                    link.TutorId,
                    link.DocumentId,
                    link.Document?.Name ?? "Document",
                    AttachmentSource.Direct,
                    metadata.IsVectorized && !metadata.IsStale,
                    metadata.EmbeddingModelId,
                    metadata.EmbeddingModelName,
                    LegacyCompatibility: true));
            }

            return Result.Success<IReadOnlyList<TutorKnowledgeDocumentResult>>(
                results.OrderBy(value => value.DocumentName).ToList());
        }
    }

    public sealed record DetachTutorDocumentCommand(Guid TutorId, Guid DocumentId) : IRequest<Result>;

    public sealed class DetachTutorDocumentHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<DetachTutorDocumentCommand, Result>
    {
        public async Task<Result> Handle(DetachTutorDocumentCommand request, CancellationToken cancellationToken)
        {
            var authorization = await AuthorizeMutationAsync(
                tutorRepository,
                membership,
                currentUser,
                request.TutorId,
                cancellationToken);
            if (authorization.IsFailure)
            {
                return authorization;
            }

            var canonical = await tutorRepository.GetTutorDocumentAsync(
                request.TutorId,
                request.DocumentId,
                cancellationToken);
            if (canonical is not null)
            {
                await tutorRepository.RemoveTutorDocumentAsync(canonical, cancellationToken);
                return Result.Success();
            }

            var legacy = await tutorRepository.GetKnowledgeSourceLinkAsync(
                request.TutorId,
                request.DocumentId,
                cancellationToken);
            if (legacy is null)
            {
                return Result.Failure(new Error("TutorKnowledge.NotFound", "Tutor document attachment was not found."));
            }

            await tutorRepository.RemoveKnowledgeSourceLinkAsync(legacy, cancellationToken);
            return Result.Success();
        }
    }

    public sealed record AttachTutorDocumentFolderCommand(Guid TutorId, Guid FolderId)
        : IRequest<Result<TutorDocumentFolder>>;

    public sealed class AttachTutorDocumentFolderHandler(
        ITutorRepository tutorRepository,
        IDocumentFolderStore folders,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<AttachTutorDocumentFolderCommand, Result<TutorDocumentFolder>>
    {
        public async Task<Result<TutorDocumentFolder>> Handle(
            AttachTutorDocumentFolderCommand request,
            CancellationToken cancellationToken)
        {
            var authorization = await AuthorizeMutationAsync(
                tutorRepository,
                membership,
                currentUser,
                request.TutorId,
                cancellationToken);
            if (authorization.IsFailure)
            {
                return Result.Failure<TutorDocumentFolder>(authorization.Error);
            }

            var folder = await folders.GetByIdAsync(request.FolderId, cancellationToken);
            if (folder is null)
            {
                return Result.Failure<TutorDocumentFolder>(new Error("DocumentFolder.NotFound", "Document folder not found."));
            }
            if (folder.CreatedById != currentUser.UserId)
            {
                return Result.Failure<TutorDocumentFolder>(new Error("DocumentFolder.Forbidden", "Requesting user does not own this folder."));
            }
            if (await tutorRepository.GetTutorDocumentFolderAsync(request.TutorId, request.FolderId, cancellationToken) is not null)
            {
                return Result.Failure<TutorDocumentFolder>(new Error(
                    "TutorKnowledge.FolderAlreadyAttached",
                    "Document folder is already attached to this tutor."));
            }

            var link = new TutorDocumentFolder
            {
                TutorId = request.TutorId,
                DocumentFolderId = request.FolderId,
                DocumentFolder = folder
            };
            return Result.Success(await tutorRepository.AddTutorDocumentFolderAsync(link, cancellationToken));
        }
    }

    public sealed record ListTutorDocumentFoldersQuery(Guid TutorId)
        : IRequest<Result<IReadOnlyList<TutorKnowledgeFolderResult>>>;

    public sealed class ListTutorDocumentFoldersHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListTutorDocumentFoldersQuery, Result<IReadOnlyList<TutorKnowledgeFolderResult>>>
    {
        public async Task<Result<IReadOnlyList<TutorKnowledgeFolderResult>>> Handle(
            ListTutorDocumentFoldersQuery request,
            CancellationToken cancellationToken)
        {
            var authorization = await AuthorizeReadAsync(
                tutorRepository,
                membership,
                currentUser,
                request.TutorId,
                cancellationToken);
            if (authorization.IsFailure)
            {
                return Result.Failure<IReadOnlyList<TutorKnowledgeFolderResult>>(authorization.Error);
            }

            var links = await tutorRepository.ListTutorDocumentFoldersAsync(request.TutorId, cancellationToken);
            return Result.Success<IReadOnlyList<TutorKnowledgeFolderResult>>(links.Select(link =>
                new TutorKnowledgeFolderResult(
                    link.Id,
                    link.TutorId,
                    link.DocumentFolderId,
                    link.DocumentFolder?.Name ?? "Folder")).ToList());
        }
    }

    public sealed record GetTutorKnowledgeOverviewQuery(Guid TutorId)
        : IRequest<Result<TutorKnowledgeOverviewResult>>;

    public sealed class GetTutorKnowledgeOverviewHandler(
        ITutorRepository tutorRepository,
        IDocumentStore documents,
        IDocumentFolderStore folders,
        IDocumentVectorizationReader vectorization,
        IRevisionEffectiveSetReader effectiveSetReader,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<GetTutorKnowledgeOverviewQuery, Result<TutorKnowledgeOverviewResult>>
    {
        public async Task<Result<TutorKnowledgeOverviewResult>> Handle(
            GetTutorKnowledgeOverviewQuery request,
            CancellationToken cancellationToken)
        {
            var authorization = await AuthorizeReadAsync(
                tutorRepository, membership, currentUser, request.TutorId, cancellationToken);
            if (authorization.IsFailure)
                return Result.Failure<TutorKnowledgeOverviewResult>(authorization.Error);

            var tutor = await tutorRepository.GetByIdWithDetailsAsync(request.TutorId, cancellationToken);
            if (tutor is null)
                return Result.Failure<TutorKnowledgeOverviewResult>(new Error("Tutor.NotFound", "Tutor not found."));

            var directLinks = await tutorRepository.ListTutorDocumentsAsync(tutor.Id, cancellationToken);
            var legacyLinks = await tutorRepository.ListKnowledgeSourceLinksAsync(tutor.Id, cancellationToken);
            var folderLinks = await tutorRepository.ListTutorDocumentFoldersAsync(tutor.Id, cancellationToken);
            var directIds = directLinks.Select(link => link.DocumentId)
                .Concat(legacyLinks.Select(link => link.DocumentId)).ToHashSet();
            var attachedFolderIds = folderLinks.Select(link => link.DocumentFolderId).ToHashSet();

            var folderTree = await folders.ListByCreatorAsync(currentUser.UserId, cancellationToken);
            var effectiveFolderIds = new HashSet<Guid>(attachedFolderIds);
            var foundDescendant = true;
            while (foundDescendant)
            {
                foundDescendant = false;
                foreach (var folder in folderTree.Where(folder =>
                             folder.ParentFolderId.HasValue && effectiveFolderIds.Contains(folder.ParentFolderId.Value)))
                    foundDescendant |= effectiveFolderIds.Add(folder.Id);
            }

            var effective = await effectiveSetReader.GetEffectiveSetAsync(tutor.Id, cancellationToken);
            var queryEmbedding = tutor.ActiveRevision?.QueryEmbeddingModel;
            var effectiveResults = new List<TutorKnowledgeEffectiveDocumentResult>(effective.DocumentIds.Count);
            foreach (var documentId in effective.DocumentIds)
            {
                var document = await documents.GetByIdAsync(documentId, cancellationToken);
                var metadata = await vectorization.GetMetadataAsync(documentId, cancellationToken);
                var isDirect = directIds.Contains(documentId);
                var isFolder = document?.FolderId is { } folderId && effectiveFolderIds.Contains(folderId);
                var attachedThrough = (isDirect, isFolder) switch
                {
                    (true, true) => "Direct and folder",
                    (true, false) => "Direct",
                    (false, true) => "Folder",
                    _ => "Tutor knowledge"
                };

                var readiness = GetDocumentReadiness(metadata, queryEmbedding);
                effectiveResults.Add(new TutorKnowledgeEffectiveDocumentResult(
                    documentId,
                    document?.Name ?? "Document unavailable",
                    attachedThrough,
                    readiness.Code,
                    metadata.IsVectorized,
                    metadata.IsStale,
                    metadata.ChunkCount,
                    metadata.EmbeddingModelId,
                    metadata.EmbeddingModelName,
                    metadata.ProviderModelName,
                    metadata.EmbeddingDimensions,
                    readiness.Ready,
                    readiness.Reason));
            }

            var directResults = new List<TutorKnowledgeDocumentResult>();
            foreach (var link in directLinks)
            {
                var metadata = await vectorization.GetMetadataAsync(link.DocumentId, cancellationToken);
                directResults.Add(new TutorKnowledgeDocumentResult(
                    link.Id, link.TutorId, link.DocumentId, link.Document?.Name ?? "Document",
                    AttachmentSource.Direct, metadata.IsVectorized && !metadata.IsStale,
                    metadata.EmbeddingModelId, metadata.EmbeddingModelName, false));
            }
            foreach (var link in legacyLinks.Where(link => directLinks.All(canonical => canonical.DocumentId != link.DocumentId)))
            {
                var metadata = await vectorization.GetMetadataAsync(link.DocumentId, cancellationToken);
                directResults.Add(new TutorKnowledgeDocumentResult(
                    link.Id, link.TutorId, link.DocumentId, link.Document?.Name ?? "Document",
                    AttachmentSource.Direct, metadata.IsVectorized && !metadata.IsStale,
                    metadata.EmbeddingModelId, metadata.EmbeddingModelName, true));
            }

            var snapshot = (await tutorRepository.ListSnapshotsByTutorAsync(tutor.Id, cancellationToken))
                .Where(value => value.TutorRevisionId == tutor.ActiveRevisionId)
                .OrderByDescending(value => value.CapturedUtc)
                .FirstOrDefault();
            var snapshotMatches = snapshot is not null && SnapshotMatches(snapshot, effective);
            return Result.Success(new TutorKnowledgeOverviewResult(
                directResults.OrderBy(value => value.DocumentName).ToList(),
                folderLinks.Select(link => new TutorKnowledgeFolderResult(
                    link.Id, link.TutorId, link.DocumentFolderId, link.DocumentFolder?.Name ?? "Folder")).ToList(),
                effectiveResults.OrderBy(value => value.DocumentName).ToList(),
                effectiveResults.Count,
                tutor.ActiveRevision?.VersionNumber,
                snapshot?.Id,
                snapshotMatches,
                queryEmbedding?.Id,
                queryEmbedding?.Name,
                queryEmbedding?.ProviderModelName,
                queryEmbedding?.EmbeddingDimensions));
        }

        private static (string Code, bool Ready, string? Reason) GetDocumentReadiness(
            DocumentVectorizationMetadata metadata,
            EmbeddingModel? queryModel)
        {
            if (!metadata.IsVectorized || metadata.ChunkCount == 0)
                return ("NotVectorized", false, "Vectorize this document before it can be captured in a chat snapshot.");
            if (metadata.IsStale)
                return ("Stale", false, "The document has changed since it was vectorized. Re-vectorize it before capturing a snapshot.");
            if (queryModel is null)
                return ("QueryEmbeddingModelMissing", false, "The active revision has no query embedding model for knowledge search.");
            if (metadata.EmbeddingModelId != queryModel.Id)
                return ("EmbeddingModelMismatch", false, "This document was embedded with a different model than the active revision uses.");
            if (metadata.EmbeddingDimensions != queryModel.EmbeddingDimensions)
                return ("EmbeddingDimensionsMismatch", false, "This document's embedding dimensions do not match the active revision.");
            if (!queryModel.IsEnabled || queryModel.RuntimeEngine?.IsEnabled != true)
                return ("EmbeddingModelUnavailable", false, "The active revision's embedding model or runtime is disabled.");
            if (queryModel.RuntimeEngine.ProviderType is not (RuntimeEngineProviderType.Ollama or RuntimeEngineProviderType.OpenAI or RuntimeEngineProviderType.OpenAICompatible or RuntimeEngineProviderType.LlamaCpp))
                return ("EmbeddingModelUnavailable", false, "The active revision's embedding provider is unavailable for knowledge search.");
            return ("Ready", true, null);
        }

        private static bool SnapshotMatches(TutorConfigurationSnapshot snapshot, EffectiveDocumentSet effective)
        {
            try
            {
                var snapshotDocumentIds = JsonSerializer.Deserialize<List<Guid>>(snapshot.EffectiveDocumentIdsJson ?? "[]") ?? [];
                var snapshotBindings = JsonSerializer.Deserialize<List<EffectiveKnowledgeBinding>>(snapshot.EffectiveKnowledgeBindingJson ?? "[]") ?? [];
                return snapshotDocumentIds.Order().SequenceEqual(effective.DocumentIds.Order()) &&
                       snapshotBindings.Select(binding => (binding.DocumentId, binding.EmbeddingModelId, binding.Dimensions)).OrderBy(value => value.DocumentId).ThenBy(value => value.EmbeddingModelId).ThenBy(value => value.Dimensions)
                           .SequenceEqual(effective.KnowledgeBindings.Select(binding => (binding.DocumentId, binding.EmbeddingModelId, binding.Dimensions)).OrderBy(value => value.DocumentId).ThenBy(value => value.EmbeddingModelId).ThenBy(value => value.Dimensions));
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }

    public sealed record DetachTutorDocumentFolderCommand(Guid TutorId, Guid FolderId) : IRequest<Result>;

    public sealed class DetachTutorDocumentFolderHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<DetachTutorDocumentFolderCommand, Result>
    {
        public async Task<Result> Handle(
            DetachTutorDocumentFolderCommand request,
            CancellationToken cancellationToken)
        {
            var authorization = await AuthorizeMutationAsync(
                tutorRepository,
                membership,
                currentUser,
                request.TutorId,
                cancellationToken);
            if (authorization.IsFailure)
            {
                return authorization;
            }

            var link = await tutorRepository.GetTutorDocumentFolderAsync(
                request.TutorId,
                request.FolderId,
                cancellationToken);
            if (link is null)
            {
                return Result.Failure(new Error("TutorKnowledge.FolderNotFound", "Tutor folder attachment was not found."));
            }

            await tutorRepository.RemoveTutorDocumentFolderAsync(link, cancellationToken);
            return Result.Success();
        }
    }

    private static async Task<Result> AuthorizeReadAsync(
        ITutorRepository tutors,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser,
        Guid tutorId,
        CancellationToken cancellationToken)
    {
        if (await tutors.GetByIdAsync(tutorId, cancellationToken) is null)
        {
            return Result.Failure(new Error("Tutor.NotFound", "Tutor not found."));
        }
        return await membership.GetRoleAsync(tutorId, currentUser.UserId, cancellationToken) is null
            ? Result.Failure(new Error("Tutor.NotFound", "Tutor not found."))
            : Result.Success();
    }

    private static async Task<Result> AuthorizeMutationAsync(
        ITutorRepository tutors,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser,
        Guid tutorId,
        CancellationToken cancellationToken)
    {
        if (await tutors.GetByIdAsync(tutorId, cancellationToken) is null)
        {
            return Result.Failure(new Error("Tutor.NotFound", "Tutor not found."));
        }
        var role = await membership.GetRoleAsync(tutorId, currentUser.UserId, cancellationToken);
        return role is ResourceRole.Owner or ResourceRole.Editor
            ? Result.Success()
            : Result.Failure(new Error("Tutor.NotFound", "Tutor not found."));
    }
}
