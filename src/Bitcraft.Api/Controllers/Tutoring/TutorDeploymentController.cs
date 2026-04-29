using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Features.Tutoring;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Tutoring;

[ApiController]
[Authorize]
[Route("api/tutors/{tutorId:guid}/deployment-profile")]
[Produces("application/json")]
public class TutorDeploymentController(ISender sender) : ControllerBase
{
    /// <summary>Saves the deployment profile for a tutor owned by the authenticated user.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(DeploymentProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeploymentProfileResponse>> Save(
        Guid tutorId,
        [FromBody] SaveDeploymentProfileRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DeploymentProfileFeatures.SaveDeploymentProfileCommand(
            tutorId,
            request.Target,
            request.ConfigurationJson);
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }
}
