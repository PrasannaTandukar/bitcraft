using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Tutoring;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Tutoring;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
[Route("api/tutors/{tutorId:guid}/knowledge-sources")]
[Produces("application/json")]
public class TutorKnowledgeSourcesController(ISender sender) : ControllerBase
{
    /// <summary>Legacy compatibility route. New writes are canonical TutorDocument rows.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(KnowledgeSourceLinkResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KnowledgeSourceLinkResponse>> Link(
        Guid tutorId,
        [FromBody] LinkKnowledgeSourceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new KnowledgeSourceFeatures.LinkKnowledgeSourceCommand(tutorId, request.DocumentId), cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }

        return CreatedAtAction("GetById", "Tutors", new { id = result.Value.TutorId }, new KnowledgeSourceLinkResponse(
            result.Value.Id,
            result.Value.TutorId,
            result.Value.DocumentId,
            result.Value.CreatedUtc));
    }

    [HttpGet("~/api/tutors/{tutorId:guid}/documents")]
    [ProducesResponseType(typeof(IReadOnlyList<TutorKnowledgeDocumentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TutorKnowledgeDocumentResponse>>> ListDocuments(
        Guid tutorId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new KnowledgeSourceFeatures.ListTutorDocumentsQuery(tutorId), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(value => value.ToResponse()).ToList())
            : TutoringMapping.ToActionResult(result.Error);
    }

    [HttpPost("~/api/tutors/{tutorId:guid}/documents")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(KnowledgeSourceLinkResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<KnowledgeSourceLinkResponse>> AttachDocument(
        Guid tutorId,
        [FromBody] AttachTutorDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new KnowledgeSourceFeatures.AttachTutorDocumentCommand(tutorId, request.DocumentId),
            cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }
        return StatusCode(StatusCodes.Status201Created, new KnowledgeSourceLinkResponse(
            result.Value.Id,
            result.Value.TutorId,
            result.Value.DocumentId,
            result.Value.CreatedUtc));
    }

    [HttpDelete("~/api/tutors/{tutorId:guid}/documents/{documentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DetachDocument(
        Guid tutorId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new KnowledgeSourceFeatures.DetachTutorDocumentCommand(tutorId, documentId),
            cancellationToken);
        return result.IsSuccess ? NoContent() : TutoringMapping.ToActionResult(result.Error);
    }

    [HttpGet("~/api/tutors/{tutorId:guid}/document-folders")]
    [ProducesResponseType(typeof(IReadOnlyList<TutorKnowledgeFolderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TutorKnowledgeFolderResponse>>> ListFolders(
        Guid tutorId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new KnowledgeSourceFeatures.ListTutorDocumentFoldersQuery(tutorId), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(value => value.ToResponse()).ToList())
            : TutoringMapping.ToActionResult(result.Error);
    }

    [HttpPost("~/api/tutors/{tutorId:guid}/document-folders")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(TutorKnowledgeFolderResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<TutorKnowledgeFolderResponse>> AttachFolder(
        Guid tutorId,
        [FromBody] AttachTutorDocumentFolderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new KnowledgeSourceFeatures.AttachTutorDocumentFolderCommand(tutorId, request.FolderId),
            cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }
        return StatusCode(StatusCodes.Status201Created, new TutorKnowledgeFolderResponse(
            result.Value.Id,
            result.Value.TutorId,
            result.Value.DocumentFolderId,
            result.Value.DocumentFolder?.Name ?? "Folder"));
    }

    [HttpDelete("~/api/tutors/{tutorId:guid}/document-folders/{folderId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DetachFolder(
        Guid tutorId,
        Guid folderId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new KnowledgeSourceFeatures.DetachTutorDocumentFolderCommand(tutorId, folderId),
            cancellationToken);
        return result.IsSuccess ? NoContent() : TutoringMapping.ToActionResult(result.Error);
    }
}
