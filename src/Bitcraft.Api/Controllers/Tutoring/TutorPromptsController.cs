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
[Route("api/tutors/{tutorId:guid}/prompts")]
[Produces("application/json")]
public class TutorPromptsController(ISender sender) : ControllerBase
{
    /// <summary>Lists immutable prompt versions for a tutor, newest first.</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
    [ProducesResponseType(typeof(IReadOnlyList<PromptVersionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PromptVersionResponse>>> List(
        Guid tutorId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new PromptVersionFeatures.ListPromptVersionsQuery(tutorId), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(x => x.ToResponse()).ToList())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Gets an immutable prompt version through its owning tutor.</summary>
    [HttpGet("{promptVersionId:guid}", Name = "GetTutorPromptVersionById")]
    [Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
    [ProducesResponseType(typeof(PromptVersionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromptVersionResponse>> GetById(
        Guid tutorId,
        Guid promptVersionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new PromptVersionFeatures.GetPromptVersionQuery(tutorId, promptVersionId),
            cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Adds a prompt version to a tutor owned by the authenticated user.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(PromptVersionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromptVersionResponse>> Create(
        Guid tutorId,
        [FromBody] CreatePromptVersionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new PromptVersionFeatures.CreatePromptVersionCommand(
            tutorId,
            request.Name,
            request.Content,
            request.Notes,
            request.SetAsActive);
        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }

        return CreatedAtRoute(
            "GetTutorPromptVersionById",
            new { tutorId = result.Value.TutorId, promptVersionId = result.Value.Id },
            result.Value.ToResponse());
    }
}
