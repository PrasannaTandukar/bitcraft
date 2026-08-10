using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Documents;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Documents;
using Bitcraft.Application.Features.Knowledge;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Documents;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
[Route("api/documents")]
[Produces("application/json")]
public class DocumentsController(ISender sender, DocumentIngestionLimits limits) : ControllerBase
{
    /// <summary>Lists source documents created by the authenticated user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<DocumentSummaryResponse>>> List(CancellationToken cancellationToken)
    {
        var documents = await sender.Send(new DocumentFeatures.ListDocumentsQuery(), cancellationToken);
        return Ok(documents.Select(x => x.ToSummaryResponse()).ToList());
    }

    /// <summary>Gets a source document by id when owned by the authenticated user.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DocumentFeatures.GetDocumentByIdQuery(id), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToDetailResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Returns extraction readiness and vectorization metadata without loading embedding vectors.</summary>
    [HttpGet("{id:guid}/vectorization-status")]
    [ProducesResponseType(typeof(DocumentVectorizationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentVectorizationStatusResponse>> GetVectorizationStatus(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new DocumentFeatures.GetDocumentVectorizationStatusQuery(id),
            cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Creates a source document for later vectorization.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(DocumentDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DocumentDetailResponse>> Create(
        [FromBody] CreateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DocumentFeatures.CreateDocumentCommand(
            request.Name,
            request.SourcePath,
            request.MimeType,
            request.RawText,
            request.FolderId);
        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }

        var response = result.Value.ToDetailResponse();
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    /// <summary>Uploads a supported file and extracts its text on the server.</summary>
    [HttpPost("upload")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DocumentDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<DocumentDetailResponse>> Upload(
        [FromForm] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            return TutoringMapping.ToActionResult(new Error("Document.FileRequired", "A file is required."));
        }

        var content = await ReadBoundedFileAsync(request.File, cancellationToken);
        if (content.IsFailure)
        {
            return TutoringMapping.ToActionResult(content.Error);
        }

        var result = await sender.Send(new DocumentFeatures.UploadDocumentCommand(
            request.File.FileName,
            request.File.ContentType,
            request.File.Length,
            content.Value,
            request.FolderId), cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }

        var response = result.Value.ToDetailResponse();
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    /// <summary>Replaces a document's source file and clears stale chunks after extraction succeeds.</summary>
    [HttpPut("{id:guid}/content")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DocumentDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<DocumentDetailResponse>> ReplaceContent(
        Guid id,
        [FromForm] ReplaceDocumentContentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            return TutoringMapping.ToActionResult(new Error("Document.FileRequired", "A file is required."));
        }

        var content = await ReadBoundedFileAsync(request.File, cancellationToken);
        if (content.IsFailure)
        {
            return TutoringMapping.ToActionResult(content.Error);
        }

        var result = await sender.Send(new DocumentFeatures.ReplaceDocumentContentCommand(
            id,
            request.File.FileName,
            request.File.ContentType,
            request.File.Length,
            content.Value), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToDetailResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Updates a source document owned by the authenticated user.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(DocumentDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDetailResponse>> Update(
        Guid id,
        [FromBody] UpdateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DocumentFeatures.UpdateDocumentCommand(
            id,
            request.Name,
            request.SourcePath,
            request.MimeType,
            request.RawText);
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToDetailResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Deletes a source document owned by the authenticated user.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DocumentFeatures.DeleteDocumentCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Rebuilds knowledge chunks for a source document owned by the authenticated user.</summary>
    [HttpPost("{id:guid}/vectorize")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(VectorizeDocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VectorizeDocumentResponse>> Vectorize(
        Guid id,
        [FromBody] VectorizeDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new KnowledgeFeatures.VectorizeDocumentCommand(
            id,
            request.EmbeddingProvider,
            request.EmbeddingModel,
            request.MaxChunkLength,
            request.EmbeddingModelId,
            request.ChunkOverlap);
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Moves a document to a folder (or to the user root when <c>folderId</c> is null).</summary>
    [HttpPatch("{id:guid}/folder")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(DocumentDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDetailResponse>> Move(
        Guid id,
        [FromBody] MoveDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DocumentFeatures.MoveDocumentToFolderCommand(id, request.FolderId), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToDetailResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    private async Task<Result<ReadOnlyMemory<byte>>> ReadBoundedFileAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            return Result.Failure<ReadOnlyMemory<byte>>(new Error("Document.FileEmpty", "The selected file is empty."));
        }

        if (file.Length > limits.MaxUploadBytes)
        {
            return Result.Failure<ReadOnlyMemory<byte>>(new Error(
                "Document.FileTooLarge",
                $"The selected file exceeds the {limits.MaxUploadBytes} byte upload limit."));
        }

        await using var input = file.OpenReadStream();
        await using var output = new MemoryStream((int)Math.Min(file.Length, limits.MaxUploadBytes));
        var buffer = new byte[81920];

        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (output.Length + read > limits.MaxUploadBytes)
            {
                return Result.Failure<ReadOnlyMemory<byte>>(new Error(
                    "Document.FileTooLarge",
                    $"The selected file exceeds the {limits.MaxUploadBytes} byte upload limit."));
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return output.Length == 0
            ? Result.Failure<ReadOnlyMemory<byte>>(new Error("Document.FileEmpty", "The selected file is empty."))
            : Result.Success<ReadOnlyMemory<byte>>(output.ToArray());
    }
}
