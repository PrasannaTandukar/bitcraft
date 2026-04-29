using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Documents;
using MediatR;

namespace Bitcraft.Application.Features.Documents;

/// <summary>
/// Document authoring and embedding use cases. Manages the source-of-truth
/// document base that feeds the RAG knowledge retrieval pipeline. Read-side
/// retrieval lives under <see cref="Knowledge.KnowledgeFeatures"/>.
/// </summary>
public static class DocumentFeatures
{
    public sealed record CreateDocumentCommand(
        string Name,
        string? SourcePath,
        string? MimeType,
        string? RawText) : IRequest<Result<Document>>;

    public sealed class CreateDocumentHandler(IDocumentStore documentStore, ICurrentUserContext currentUser)
        : IRequestHandler<CreateDocumentCommand, Result<Document>>
    {
        public async Task<Result<Document>> Handle(CreateDocumentCommand request, CancellationToken cancellationToken)
        {
            _ = currentUser.UserId;

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Result.Failure<Document>(new Error("Document.NameRequired", "Document name is required."));
            }

            var document = new Document
            {
                Name = request.Name.Trim(),
                SourcePath = string.IsNullOrWhiteSpace(request.SourcePath) ? null : request.SourcePath.Trim(),
                MimeType = string.IsNullOrWhiteSpace(request.MimeType) ? null : request.MimeType.Trim(),
                RawText = request.RawText
            };

            var created = await documentStore.AddAsync(document, cancellationToken);
            return Result.Success(created);
        }
    }

    public sealed record UpdateDocumentCommand(
        Guid DocumentId,
        string Name,
        string? SourcePath,
        string? MimeType,
        string? RawText) : IRequest<Result<Document>>;

    public sealed class UpdateDocumentHandler(IDocumentStore documentStore, ICurrentUserContext currentUser)
        : IRequestHandler<UpdateDocumentCommand, Result<Document>>
    {
        public async Task<Result<Document>> Handle(UpdateDocumentCommand request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Result.Failure<Document>(new Error("Document.NameRequired", "Document name is required."));
            }

            var document = await documentStore.GetByIdAsync(request.DocumentId, cancellationToken);
            if (document is null)
            {
                return Result.Failure<Document>(new Error("Document.NotFound", $"Document '{request.DocumentId}' not found."));
            }

            if (document.CreatedById != userId)
            {
                return Result.Failure<Document>(new Error("Document.Forbidden", "Requesting user does not own this document."));
            }

            document.Name = request.Name.Trim();
            document.SourcePath = string.IsNullOrWhiteSpace(request.SourcePath) ? null : request.SourcePath.Trim();
            document.MimeType = string.IsNullOrWhiteSpace(request.MimeType) ? null : request.MimeType.Trim();
            document.RawText = request.RawText;

            await documentStore.UpdateAsync(document, cancellationToken);
            return Result.Success(document);
        }
    }

    public sealed record DeleteDocumentCommand(Guid DocumentId) : IRequest<Result>;

    public sealed class DeleteDocumentHandler(IDocumentStore documentStore, ICurrentUserContext currentUser)
        : IRequestHandler<DeleteDocumentCommand, Result>
    {
        public async Task<Result> Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;

            var document = await documentStore.GetByIdAsync(request.DocumentId, cancellationToken);
            if (document is null)
            {
                return Result.Failure(new Error("Document.NotFound", $"Document '{request.DocumentId}' not found."));
            }

            if (document.CreatedById != userId)
            {
                return Result.Failure(new Error("Document.Forbidden", "Requesting user does not own this document."));
            }

            await documentStore.DeleteAsync(document, cancellationToken);
            return Result.Success();
        }
    }

    public sealed record GetDocumentByIdQuery(Guid DocumentId) : IRequest<Result<Document>>;

    public sealed class GetDocumentByIdHandler(IDocumentStore documentStore, ICurrentUserContext currentUser)
        : IRequestHandler<GetDocumentByIdQuery, Result<Document>>
    {
        public async Task<Result<Document>> Handle(GetDocumentByIdQuery request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;

            var document = await documentStore.GetByIdAsync(request.DocumentId, cancellationToken);
            if (document is null)
            {
                return Result.Failure<Document>(new Error("Document.NotFound", $"Document '{request.DocumentId}' not found."));
            }

            return document.CreatedById == userId
                ? Result.Success(document)
                : Result.Failure<Document>(new Error("Document.Forbidden", "Requesting user does not own this document."));
        }
    }

    public sealed record ListDocumentsQuery : IRequest<List<Document>>;

    public sealed class ListDocumentsHandler(IDocumentStore documentStore, ICurrentUserContext currentUser)
        : IRequestHandler<ListDocumentsQuery, List<Document>>
    {
        public Task<List<Document>> Handle(ListDocumentsQuery request, CancellationToken cancellationToken)
        {
            return documentStore.ListByCreatorAsync(currentUser.UserId, cancellationToken);
        }
    }
}
