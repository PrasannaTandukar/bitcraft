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
[Route("api/tutors/{tutorId:guid}/snapshots")]
[Produces("application/json")]
public class TutorSnapshotsController(ISender sender) : ControllerBase
{
    /// <summary>Lists immutable activation snapshots for an authorized tutor member.</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
    [ProducesResponseType(typeof(IReadOnlyList<TutorConfigurationSnapshotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TutorConfigurationSnapshotResponse>>> List(
        Guid tutorId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ConfigurationSnapshotFeatures.ListConfigurationSnapshotsQuery(tutorId),
            cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(x => x.ToResponse()).ToList())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Gets one immutable activation snapshot through its owning tutor.</summary>
    [HttpGet("{snapshotId:guid}", Name = "GetTutorSnapshotById")]
    [Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
    [ProducesResponseType(typeof(TutorConfigurationSnapshotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorConfigurationSnapshotResponse>> GetById(
        Guid tutorId,
        Guid snapshotId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ConfigurationSnapshotFeatures.GetConfigurationSnapshotQuery(tutorId, snapshotId),
            cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Captures an immutable configuration snapshot for a tutor.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
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

        return CreatedAtRoute(
            "GetTutorSnapshotById",
            new { tutorId = result.Value.TutorId, snapshotId = result.Value.Id },
            result.Value.ToResponse());
    }

    /// <summary>Captures a new immutable knowledge snapshot for the existing active revision.</summary>
    [HttpPost("active-revision")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(TutorConfigurationSnapshotResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorConfigurationSnapshotResponse>> CaptureActiveRevision(
        Guid tutorId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ConfigurationSnapshotFeatures.CaptureActiveRevisionSnapshotCommand(tutorId),
            cancellationToken);
        if (result.IsFailure) return TutoringMapping.ToActionResult(result.Error);
        return CreatedAtRoute(
            "GetTutorSnapshotById",
            new { tutorId, snapshotId = result.Value.Id },
            result.Value.ToResponse());
    }
}
