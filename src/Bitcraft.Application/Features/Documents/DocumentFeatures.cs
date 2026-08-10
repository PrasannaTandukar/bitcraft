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
    public sealed record UploadDocumentCommand(
        string FileName,
        string? MimeType,
        long FileSizeBytes,
        ReadOnlyMemory<byte> Content,
        Guid? FolderId) : IRequest<Result<Document>>;

    public sealed class UploadDocumentHandler(
        IDocumentStore documentStore,
        IDocumentFolderStore folderStore,
        IDocumentTextExtractionService extractionService,
        DocumentIngestionLimits limits,
        ICurrentUserContext currentUser)
        : IRequestHandler<UploadDocumentCommand, Result<Document>>
    {
        public async Task<Result<Document>> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
        {
            var validation = ValidateUploadedFile(request.FileName, request.FileSizeBytes, request.Content.Length, limits);
            if (validation.IsFailure)
            {
                return Result.Failure<Document>(validation.Error);
            }

            var userId = currentUser.UserId;
            var folderValidation = await ValidateFolderAsync(request.FolderId, userId, folderStore, cancellationToken);
            if (folderValidation.IsFailure)
            {
                return Result.Failure<Document>(folderValidation.Error);
            }

            var safeFileName = Path.GetFileName(request.FileName.Trim());
            var extraction = await extractionService.ExtractAsync(
                new DocumentTextExtractionRequest(safeFileName, request.MimeType, request.Content),
                cancellationToken);
            if (extraction.IsFailure)
            {
                return Result.Failure<Document>(extraction.Error);
            }

            var textValidation = ValidateExtractedText(extraction.Value.Text, limits);
            if (textValidation.IsFailure)
            {
                return Result.Failure<Document>(textValidation.Error);
            }

            var document = new Document
            {
                Name = safeFileName,
                SourcePath = null,
                MimeType = extraction.Value.DetectedContentType,
                RawText = extraction.Value.Text,
                FolderId = request.FolderId
            };
            document.UpdateMetadata(userId);
            return Result.Success(await documentStore.AddAsync(document, cancellationToken));
        }
    }

    public sealed record ReplaceDocumentContentCommand(
        Guid DocumentId,
        string FileName,
        string? MimeType,
        long FileSizeBytes,
        ReadOnlyMemory<byte> Content) : IRequest<Result<Document>>;

    public sealed class ReplaceDocumentContentHandler(
        IDocumentStore documentStore,
        IDocumentContentStore contentStore,
        IDocumentTextExtractionService extractionService,
        DocumentIngestionLimits limits,
        ICurrentUserContext currentUser)
        : IRequestHandler<ReplaceDocumentContentCommand, Result<Document>>
    {
        public async Task<Result<Document>> Handle(
            ReplaceDocumentContentCommand request,
            CancellationToken cancellationToken)
        {
            var validation = ValidateUploadedFile(request.FileName, request.FileSizeBytes, request.Content.Length, limits);
            if (validation.IsFailure)
            {
                return Result.Failure<Document>(validation.Error);
            }

            var document = await documentStore.GetByIdAsync(request.DocumentId, cancellationToken);
            if (document is null)
            {
                return Result.Failure<Document>(new Error("Document.NotFound", "Document not found."));
            }

            if (document.CreatedById != currentUser.UserId)
            {
                return Result.Failure<Document>(new Error("Document.Forbidden", "Requesting user does not own this document."));
            }

            var safeFileName = Path.GetFileName(request.FileName.Trim());
            var extraction = await extractionService.ExtractAsync(
                new DocumentTextExtractionRequest(safeFileName, request.MimeType, request.Content),
                cancellationToken);
            if (extraction.IsFailure)
            {
                return Result.Failure<Document>(extraction.Error);
            }

            var textValidation = ValidateExtractedText(extraction.Value.Text, limits);
            if (textValidation.IsFailure)
            {
                return Result.Failure<Document>(textValidation.Error);
            }

            document.Name = safeFileName;
            document.SourcePath = null;
            document.MimeType = extraction.Value.DetectedContentType;
            document.RawText = extraction.Value.Text;
            document.UpdateMetadata(currentUser.UserId);
            await contentStore.UpdateContentAndClearChunksAsync(document, cancellationToken);
            return Result.Success(document);
        }
    }

    public sealed record CreateDocumentCommand(
        string Name,
        string? SourcePath,
        string? MimeType,
        string? RawText,
        Guid? FolderId = null) : IRequest<Result<Document>>;

    public sealed class CreateDocumentHandler(
        IDocumentStore documentStore,
        IDocumentFolderStore folderStore,
        ICurrentUserContext currentUser)
        : IRequestHandler<CreateDocumentCommand, Result<Document>>
    {
        public async Task<Result<Document>> Handle(CreateDocumentCommand request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Result.Failure<Document>(new Error("Document.NameRequired", "Document name is required."));
            }

            if (request.FolderId is { } folderId)
            {
                var folder = await folderStore.GetByIdAsync(folderId, cancellationToken);
                if (folder is null)
                {
                    return Result.Failure<Document>(new Error("DocumentFolder.NotFound", $"Folder '{folderId}' not found."));
                }
                if (folder.CreatedById != userId)
                {
                    return Result.Failure<Document>(new Error("DocumentFolder.Forbidden", "Requesting user does not own folder."));
                }
            }

            var document = new Document
            {
                Name = request.Name.Trim(),
                SourcePath = string.IsNullOrWhiteSpace(request.SourcePath) ? null : request.SourcePath.Trim(),
                MimeType = string.IsNullOrWhiteSpace(request.MimeType) ? null : request.MimeType.Trim(),
                RawText = request.RawText,
                FolderId = request.FolderId
            };
            document.UpdateMetadata(userId);
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

    public sealed class UpdateDocumentHandler(
        IDocumentStore documentStore,
        IDocumentContentStore contentStore,
        ICurrentUserContext currentUser)
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
            var rawTextChanged = !string.Equals(document.RawText, request.RawText, StringComparison.Ordinal);
            document.RawText = request.RawText;

            if (rawTextChanged)
            {
                await contentStore.UpdateContentAndClearChunksAsync(document, cancellationToken);
            }
            else
            {
                await documentStore.UpdateAsync(document, cancellationToken);
            }
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

    public sealed record GetDocumentVectorizationStatusQuery(Guid DocumentId)
        : IRequest<Result<DocumentVectorizationStatus>>;

    public sealed class GetDocumentVectorizationStatusHandler(
        IDocumentStore documentStore,
        IDocumentVectorizationReader vectorizationReader,
        ICurrentUserContext currentUser)
        : IRequestHandler<GetDocumentVectorizationStatusQuery, Result<DocumentVectorizationStatus>>
    {
        public async Task<Result<DocumentVectorizationStatus>> Handle(
            GetDocumentVectorizationStatusQuery request,
            CancellationToken cancellationToken)
        {
            var document = await documentStore.GetByIdAsync(request.DocumentId, cancellationToken);
            if (document is null)
            {
                return Result.Failure<DocumentVectorizationStatus>(new Error("Document.NotFound", "Document not found."));
            }

            if (document.CreatedById != currentUser.UserId)
            {
                return Result.Failure<DocumentVectorizationStatus>(new Error(
                    "Document.Forbidden",
                    "Requesting user does not own this document."));
            }

            var metadata = await vectorizationReader.GetMetadataAsync(document.Id, cancellationToken);
            var rawTextLength = document.RawText?.Length ?? 0;
            return Result.Success(new DocumentVectorizationStatus(
                document.Id,
                !string.IsNullOrWhiteSpace(document.RawText),
                rawTextLength,
                metadata.IsVectorized,
                metadata.IsStale,
                metadata.ChunkCount,
                metadata.LastVectorizedUtc,
                metadata.EmbeddingModelId,
                metadata.EmbeddingModelName,
                metadata.ProviderModelName,
                metadata.EmbeddingProvider,
                metadata.EmbeddingDimensions));
        }
    }

    public sealed record MoveDocumentToFolderCommand(Guid DocumentId, Guid? FolderId) : IRequest<Result<Document>>;

    public sealed class MoveDocumentToFolderHandler(
        IDocumentStore documentStore,
        IDocumentFolderStore folderStore,
        ICurrentUserContext currentUser)
        : IRequestHandler<MoveDocumentToFolderCommand, Result<Document>>
    {
        public async Task<Result<Document>> Handle(MoveDocumentToFolderCommand request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;
            var document = await documentStore.GetByIdAsync(request.DocumentId, cancellationToken);
            if (document is null)
            {
                return Result.Failure<Document>(new Error("Document.NotFound", $"Document '{request.DocumentId}' not found."));
            }
            if (document.CreatedById != userId)
            {
                return Result.Failure<Document>(new Error("Document.Forbidden", "Requesting user does not own this document."));
            }
            if (request.FolderId is { } folderId)
            {
                var folder = await folderStore.GetByIdAsync(folderId, cancellationToken);
                if (folder is null)
                {
                    return Result.Failure<Document>(new Error("DocumentFolder.NotFound", $"Folder '{folderId}' not found."));
                }
                if (folder.CreatedById != userId)
                {
                    return Result.Failure<Document>(new Error("DocumentFolder.Forbidden", "Requesting user does not own folder."));
                }
            }

            document.FolderId = request.FolderId;
            document.UpdateMetadata(userId);
            await documentStore.UpdateAsync(document, cancellationToken);
            return Result.Success(document);
        }
    }

    private static Result ValidateUploadedFile(
        string fileName,
        long declaredSize,
        int contentLength,
        DocumentIngestionLimits limits)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Result.Failure(new Error("Document.InvalidFileName", "A valid file name is required."));
        }

        var trimmed = fileName.Trim();
        var safeFileName = Path.GetFileName(trimmed);
        if (safeFileName.Length == 0 ||
            safeFileName.Length > 200 ||
            !string.Equals(safeFileName, trimmed, StringComparison.Ordinal) ||
            trimmed.Contains('/') ||
            trimmed.Contains('\\') ||
            safeFileName.Any(char.IsControl))
        {
            return Result.Failure(new Error("Document.InvalidFileName", "The file name is invalid."));
        }

        if (declaredSize <= 0 || contentLength == 0)
        {
            return Result.Failure(new Error("Document.FileEmpty", "The selected file is empty."));
        }

        if (declaredSize > limits.MaxUploadBytes || contentLength > limits.MaxUploadBytes)
        {
            return Result.Failure(new Error(
                "Document.FileTooLarge",
                $"The selected file exceeds the {limits.MaxUploadBytes} byte upload limit."));
        }

        if (!DocumentFileTypes.IsSupported(safeFileName))
        {
            return Result.Failure(new Error(
                "Document.UnsupportedFileType",
                "Supported file types are TXT, Markdown, CSV, JSON, PDF, and DOCX."));
        }

        return Result.Success();
    }

    private static Result ValidateExtractedText(string? text, DocumentIngestionLimits limits)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result.Failure(new Error(
                "Document.NoExtractableText",
                "The file did not contain extractable text. OCR is not supported."));
        }

        return text.Length > limits.MaxExtractedCharacters
            ? Result.Failure(new Error(
                "Document.ExtractedTextTooLarge",
                $"Extracted text exceeds the {limits.MaxExtractedCharacters} character limit."))
            : Result.Success();
    }

    private static async Task<Result> ValidateFolderAsync(
        Guid? folderId,
        Guid userId,
        IDocumentFolderStore folderStore,
        CancellationToken cancellationToken)
    {
        if (folderId is not { } id)
        {
            return Result.Success();
        }

        var folder = await folderStore.GetByIdAsync(id, cancellationToken);
        if (folder is null)
        {
            return Result.Failure(new Error("DocumentFolder.NotFound", "Folder not found."));
        }

        return folder.CreatedById == userId
            ? Result.Success()
            : Result.Failure(new Error("DocumentFolder.Forbidden", "Requesting user does not own folder."));
    }
}
