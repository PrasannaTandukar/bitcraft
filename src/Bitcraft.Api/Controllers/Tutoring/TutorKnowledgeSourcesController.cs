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
[Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
[Route("api/tutors/{tutorId:guid}/knowledge-sources")]
[Produces("application/json")]
public class TutorKnowledgeSourcesController(ISender sender) : ControllerBase
{
    /// <summary>Links an owned source document to a tutor's retrieval set.</summary>
    [HttpPost]
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

        return CreatedAtAction("GetById", "Tutors", new { id = result.Value.TutorId }, result.Value.ToResponse());
    }
}
