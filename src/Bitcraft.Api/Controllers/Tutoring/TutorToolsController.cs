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
[Route("api/tutors/{tutorId:guid}/tools")]
[Produces("application/json")]
public class TutorToolsController(ISender sender) : ControllerBase
{
    /// <summary>Registers a tool definition for a tutor owned by the authenticated user.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ToolDefinitionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ToolDefinitionResponse>> Register(
        Guid tutorId,
        [FromBody] RegisterToolDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ToolDefinitionFeatures.RegisterToolDefinitionCommand(
            tutorId,
            request.Name,
            request.Description,
            request.JsonSchema,
            request.ExecutionUrl);
        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }

        return CreatedAtAction("GetById", "Tutors", new { id = result.Value.TutorId }, result.Value.ToResponse());
    }
}
