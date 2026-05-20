using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Documents;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
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
public class DocumentsController(ISender sender) : ControllerBase
{
    /// <summary>Lists source documents created by the authenticated user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<DocumentResponse>>> List(CancellationToken cancellationToken)
    {
        var documents = await sender.Send(new DocumentFeatures.ListDocumentsQuery(), cancellationToken);
        return Ok(documents.Select(x => x.ToResponse()).ToList());
    }

    /// <summary>Gets a source document by id when owned by the authenticated user.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DocumentFeatures.GetDocumentByIdQuery(id), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Creates a source document for later vectorization.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DocumentResponse>> Create(
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

        var response = result.Value.ToResponse();
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    /// <summary>Updates a source document owned by the authenticated user.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentResponse>> Update(
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
            ? Ok(result.Value.ToResponse())
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
            request.EmbeddingModelId);
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Moves a document to a folder (or to the user root when <c>folderId</c> is null).</summary>
    [HttpPatch("{id:guid}/folder")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentResponse>> Move(
        Guid id,
        [FromBody] MoveDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DocumentFeatures.MoveDocumentToFolderCommand(id, request.FolderId), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }
}
