using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Documents;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Documents;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Documents;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
[Route("api/document-folders")]
[Produces("application/json")]
public class DocumentFoldersController(ISender sender) : ControllerBase
{
    /// <summary>Lists document folders owned by the authenticated user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentFolderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<DocumentFolderResponse>>> List(CancellationToken cancellationToken)
    {
        var folders = await sender.Send(new DocumentFolderFeatures.ListDocumentFoldersQuery(), cancellationToken);
        return Ok(folders.Select(x => x.ToResponse()).ToList());
    }

    /// <summary>Creates a folder under the optional parent. Pass <c>null</c> for root-level placement.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(DocumentFolderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentFolderResponse>> Create(
        [FromBody] CreateDocumentFolderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DocumentFolderFeatures.CreateDocumentFolderCommand(request.Name, request.ParentFolderId), cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }
        var response = result.Value.ToResponse();
        return CreatedAtAction(nameof(List), new { id = response.Id }, response);
    }

    /// <summary>Renames a folder owned by the authenticated user.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(DocumentFolderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentFolderResponse>> Rename(
        Guid id,
        [FromBody] RenameDocumentFolderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DocumentFolderFeatures.RenameDocumentFolderCommand(id, request.Name), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Moves a folder under a new parent (or to root when <c>parentFolderId</c> is null).</summary>
    [HttpPatch("{id:guid}/parent")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(DocumentFolderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentFolderResponse>> Move(
        Guid id,
        [FromBody] MoveDocumentFolderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DocumentFolderFeatures.MoveDocumentFolderCommand(id, request.ParentFolderId), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Deletes an empty folder owned by the authenticated user.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DocumentFolderFeatures.DeleteDocumentFolderCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : TutoringMapping.ToActionResult(result.Error);
    }
}
