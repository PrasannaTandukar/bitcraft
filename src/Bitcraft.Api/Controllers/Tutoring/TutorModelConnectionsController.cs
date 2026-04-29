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
[Route("api/tutors/{tutorId:guid}/model-connection")]
[Produces("application/json")]
public class TutorModelConnectionsController(ISender sender) : ControllerBase
{
    /// <summary>Saves the model connection for a tutor owned by the authenticated user.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(TutorModelConnectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorModelConnectionResponse>> Save(
        Guid tutorId,
        [FromBody] SaveTutorModelConnectionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new TutorModelConnectionFeatures.SaveTutorModelConnectionCommand(
            tutorId,
            request.ModelCatalogEntryId,
            request.OverrideSettingsJson);
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }
}
