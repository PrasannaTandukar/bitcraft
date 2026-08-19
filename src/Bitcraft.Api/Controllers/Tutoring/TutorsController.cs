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
[Route("api/tutors")]
[Produces("application/json")]
public class TutorsController(ISender sender) : ControllerBase
{
    /// <summary>Lists tutors owned by the authenticated user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TutorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<TutorResponse>>> List(CancellationToken cancellationToken)
    {
        var tutors = await sender.Send(new TutorFeatures.ListTutorsQuery(), cancellationToken);
        return Ok(tutors.Select(t => t.ToResponse()).ToList());
    }

    /// <summary>Gets a tutor by id when owned by the authenticated user.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TutorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TutorFeatures.GetTutorByIdQuery(id), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Creates a tutor owned by the authenticated user.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(TutorResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TutorResponse>> Create([FromBody] CreateTutorRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TutorFeatures.CreateTutorCommand(request.Name, request.Description), cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }

        var response = result.Value.ToResponse();
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    /// <summary>Updates tutor metadata and active prompt selection.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(TutorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorResponse>> Update(
        Guid id,
        [FromBody] UpdateTutorRequest request,
        CancellationToken cancellationToken)
    {
        var command = new TutorFeatures.UpdateTutorCommand(
            id,
            request.Name,
            request.Description,
            request.Status,
            request.ActivePromptVersionId);
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Deletes a tutor owned by the authenticated user.</summary>
    /// <remarks>Requires the EditorOrAdmin policy plus Editor-or-Owner membership on the tutor.</remarks>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TutorFeatures.DeleteTutorCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : TutoringMapping.ToActionResult(result.Error);
    }
}
