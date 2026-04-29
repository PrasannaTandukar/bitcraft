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
[Route("api/tutors/{tutorId:guid}/snapshots")]
[Produces("application/json")]
public class TutorSnapshotsController(ISender sender) : ControllerBase
{
    /// <summary>Captures an immutable configuration snapshot for a tutor.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TutorConfigurationSnapshotResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorConfigurationSnapshotResponse>> Create(Guid tutorId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ConfigurationSnapshotFeatures.CreateConfigurationSnapshotCommand(tutorId), cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }

        return CreatedAtAction("GetById", "Tutors", new { id = result.Value.TutorId }, result.Value.ToResponse());
    }
}
