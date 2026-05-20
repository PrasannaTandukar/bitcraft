using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Documents;
using MediatR;

namespace Bitcraft.Application.Features.Documents;

/// <summary>
/// Per-user document folder use cases (D25 step 5). All operations are scoped by
/// <see cref="Bitcraft.Domain.Common.BaseModel.CreatedById"/>; sharing of documents
/// across users still happens at the <c>KnowledgeSourceLink</c> level.
/// </summary>
public static class DocumentFolderFeatures
{
    public sealed record CreateDocumentFolderCommand(string Name, Guid? ParentFolderId)
        : IRequest<Result<DocumentFolder>>;

    public sealed class CreateDocumentFolderHandler(
        IDocumentFolderStore folderStore,
        ICurrentUserContext currentUser)
        : IRequestHandler<CreateDocumentFolderCommand, Result<DocumentFolder>>
    {
        public async Task<Result<DocumentFolder>> Handle(CreateDocumentFolderCommand request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Result.Failure<DocumentFolder>(new Error("DocumentFolder.NameRequired", "Folder name is required."));
            }

            if (request.ParentFolderId is { } parentId)
            {
                var parent = await folderStore.GetByIdAsync(parentId, cancellationToken);
                if (parent is null)
                {
                    return Result.Failure<DocumentFolder>(new Error("DocumentFolder.ParentNotFound", $"Parent folder '{parentId}' not found."));
                }
                if (parent.CreatedById != userId)
                {
                    return Result.Failure<DocumentFolder>(new Error("DocumentFolder.Forbidden", "Requesting user does not own parent folder."));
                }
            }

            var folder = new DocumentFolder
            {
                Name = request.Name.Trim(),
                ParentFolderId = request.ParentFolderId
            };
            folder.UpdateMetadata(userId);
            var created = await folderStore.AddAsync(folder, cancellationToken);
            return Result.Success(created);
        }
    }

    public sealed record RenameDocumentFolderCommand(Guid FolderId, string Name)
        : IRequest<Result<DocumentFolder>>;

    public sealed class RenameDocumentFolderHandler(
        IDocumentFolderStore folderStore,
        ICurrentUserContext currentUser)
        : IRequestHandler<RenameDocumentFolderCommand, Result<DocumentFolder>>
    {
        public async Task<Result<DocumentFolder>> Handle(RenameDocumentFolderCommand request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Result.Failure<DocumentFolder>(new Error("DocumentFolder.NameRequired", "Folder name is required."));
            }

            var folder = await folderStore.GetByIdAsync(request.FolderId, cancellationToken);
            if (folder is null)
            {
                return Result.Failure<DocumentFolder>(new Error("DocumentFolder.NotFound", $"Folder '{request.FolderId}' not found."));
            }
            if (folder.CreatedById != userId)
            {
                return Result.Failure<DocumentFolder>(new Error("DocumentFolder.Forbidden", "Requesting user does not own folder."));
            }

            folder.Name = request.Name.Trim();
            folder.UpdateMetadata(userId);
            await folderStore.UpdateAsync(folder, cancellationToken);
            return Result.Success(folder);
        }
    }

    public sealed record MoveDocumentFolderCommand(Guid FolderId, Guid? NewParentFolderId)
        : IRequest<Result<DocumentFolder>>;

    public sealed class MoveDocumentFolderHandler(
        IDocumentFolderStore folderStore,
        ICurrentUserContext currentUser)
        : IRequestHandler<MoveDocumentFolderCommand, Result<DocumentFolder>>
    {
        public async Task<Result<DocumentFolder>> Handle(MoveDocumentFolderCommand request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;
            var folder = await folderStore.GetByIdAsync(request.FolderId, cancellationToken);
            if (folder is null)
            {
                return Result.Failure<DocumentFolder>(new Error("DocumentFolder.NotFound", $"Folder '{request.FolderId}' not found."));
            }
            if (folder.CreatedById != userId)
            {
                return Result.Failure<DocumentFolder>(new Error("DocumentFolder.Forbidden", "Requesting user does not own folder."));
            }
            if (request.NewParentFolderId == request.FolderId)
            {
                return Result.Failure<DocumentFolder>(new Error("DocumentFolder.InvalidParent", "Folder cannot be its own parent."));
            }
            if (request.NewParentFolderId is { } newParentId)
            {
                var parent = await folderStore.GetByIdAsync(newParentId, cancellationToken);
                if (parent is null)
                {
                    return Result.Failure<DocumentFolder>(new Error("DocumentFolder.ParentNotFound", $"Parent folder '{newParentId}' not found."));
                }
                if (parent.CreatedById != userId)
                {
                    return Result.Failure<DocumentFolder>(new Error("DocumentFolder.Forbidden", "Requesting user does not own parent folder."));
                }
            }

            folder.ParentFolderId = request.NewParentFolderId;
            folder.UpdateMetadata(userId);
            await folderStore.UpdateAsync(folder, cancellationToken);
            return Result.Success(folder);
        }
    }

    public sealed record DeleteDocumentFolderCommand(Guid FolderId) : IRequest<Result>;

    public sealed class DeleteDocumentFolderHandler(
        IDocumentFolderStore folderStore,
        ICurrentUserContext currentUser)
        : IRequestHandler<DeleteDocumentFolderCommand, Result>
    {
        public async Task<Result> Handle(DeleteDocumentFolderCommand request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;
            var folder = await folderStore.GetByIdAsync(request.FolderId, cancellationToken);
            if (folder is null)
            {
                return Result.Failure(new Error("DocumentFolder.NotFound", $"Folder '{request.FolderId}' not found."));
            }
            if (folder.CreatedById != userId)
            {
                return Result.Failure(new Error("DocumentFolder.Forbidden", "Requesting user does not own folder."));
            }
            if (await folderStore.HasChildFoldersAsync(folder.Id, cancellationToken))
            {
                return Result.Failure(new Error("DocumentFolder.NotEmpty", "Folder contains sub-folders and cannot be deleted."));
            }
            if (await folderStore.HasDocumentsAsync(folder.Id, cancellationToken))
            {
                return Result.Failure(new Error("DocumentFolder.NotEmpty", "Folder contains documents and cannot be deleted."));
            }

            await folderStore.DeleteAsync(folder, cancellationToken);
            return Result.Success();
        }
    }

    public sealed record ListDocumentFoldersQuery : IRequest<List<DocumentFolder>>;

    public sealed class ListDocumentFoldersHandler(
        IDocumentFolderStore folderStore,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListDocumentFoldersQuery, List<DocumentFolder>>
    {
        public Task<List<DocumentFolder>> Handle(ListDocumentFoldersQuery request, CancellationToken cancellationToken) =>
            folderStore.ListByCreatorAsync(currentUser.UserId, cancellationToken);
    }
}
