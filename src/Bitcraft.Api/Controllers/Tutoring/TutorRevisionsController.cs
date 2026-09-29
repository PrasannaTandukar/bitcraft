using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Tutoring;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Tutoring;

/// <summary>D25 step 7 — TutorRevision CQRS endpoints (plan.erm.md §7).</summary>
[ApiController]
[Route("api/tutors/{tutorId:guid}/revisions")]
[Produces("application/json")]
public class TutorRevisionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
    [ProducesResponseType(typeof(IReadOnlyList<TutorRevisionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<TutorRevisionResponse>>> List(Guid tutorId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TutorRevisionFeatures.ListRevisionsForTutorQuery(tutorId), cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }
        return Ok(result.Value.Select(r => r.ToResponse()).ToList());
    }

    [HttpGet("{revisionId:guid}", Name = "GetTutorRevisionById")]
    [Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
    [ProducesResponseType(typeof(TutorRevisionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorRevisionResponse>> GetById(Guid tutorId, Guid revisionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TutorRevisionFeatures.GetRevisionByIdQuery(tutorId, revisionId), cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }
        return Ok(result.Value.ToResponse());
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(TutorRevisionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorRevisionResponse>> CreateDraft(Guid tutorId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TutorRevisionFeatures.CreateDraftRevisionCommand(tutorId), cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }
        var response = result.Value.ToResponse();
        return CreatedAtRoute("GetTutorRevisionById", new { tutorId, revisionId = response.Id }, response);
    }

    [HttpPut("{revisionId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(TutorRevisionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorRevisionResponse>> UpdateDraft(
        Guid tutorId,
        Guid revisionId,
        [FromBody] UpdateDraftRevisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TutorRevisionFeatures.UpdateDraftRevisionCommand(
            tutorId,
            revisionId,
            request.PromptVersionId,
            request.InteractionModelId,
            request.QueryEmbeddingModelId,
            request.InteractionParametersJson,
            request.DeploymentTarget,
            request.DeploymentConfigJson,
            request.ToolDefinitionIds), cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }
        return Ok(result.Value.ToResponse());
    }

    [HttpPost("{revisionId:guid}/publish")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(TutorRevisionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorRevisionResponse>> Publish(Guid tutorId, Guid revisionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TutorRevisionFeatures.PublishRevisionCommand(tutorId, revisionId), cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }
        return Ok(result.Value.ToResponse());
    }

    [HttpPost("{revisionId:guid}/activate")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(TutorRevisionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorRevisionResponse>> Activate(Guid tutorId, Guid revisionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TutorRevisionFeatures.ActivateRevisionCommand(tutorId, revisionId), cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }
        return Ok(result.Value.ToResponse());
    }

    [HttpPost("{revisionId:guid}/archive")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(TutorRevisionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorRevisionResponse>> Archive(Guid tutorId, Guid revisionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TutorRevisionFeatures.ArchiveRevisionCommand(tutorId, revisionId), cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }
        return Ok(result.Value.ToResponse());
    }

    [HttpDelete("{revisionId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DiscardDraft(Guid tutorId, Guid revisionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TutorRevisionFeatures.DiscardDraftRevisionCommand(tutorId, revisionId), cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }
        return NoContent();
    }
}
