using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Benchmarking;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Benchmarking;

/// <summary>
/// Model-scoped standardized DeepEval benchmark runs. Deliberately separate from the
/// tutor-scoped benchmark-runs controller to preserve Model Evaluation vs Tutor Benchmarking separation.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
[Route("api/standard-benchmark-runs")]
[Produces("application/json")]
public class StandardBenchmarkRunsController(ISender sender) : ControllerBase
{
    /// <summary>Lists the standardized benchmark suites available for runs.</summary>
    [HttpGet("suites")]
    [ProducesResponseType(typeof(IReadOnlyList<StandardBenchmarkSuiteResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<StandardBenchmarkSuiteResponse>>> ListSuites(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StandardBenchmarkFeatures.ListStandardBenchmarkSuitesQuery(), cancellationToken);
        return Ok(result.Value.Select(x => x.ToResponse()).ToList());
    }

    /// <summary>Lists standardized benchmark runs, optionally filtered by interaction model.</summary>
    /// <remarks>Archived runs are hidden unless includeArchived is true (HD-4).</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StandardBenchmarkRunResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<StandardBenchmarkRunResponse>>> List(
        [FromQuery] Guid? interactionModelId,
        [FromQuery] int limit = 50,
        [FromQuery(Name = "includeArchived")] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new StandardBenchmarkFeatures.ListStandardBenchmarkRunsQuery(interactionModelId, limit, includeArchived), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(x => x.ToResponse()).ToList())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Gets a standardized benchmark run by id.</summary>
    [HttpGet("{runId:guid}")]
    [ProducesResponseType(typeof(StandardBenchmarkRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StandardBenchmarkRunResponse>> GetById(Guid runId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StandardBenchmarkFeatures.GetStandardBenchmarkRunQuery(runId), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Gets lightweight standardized benchmark progress for polling while a run is queued or executing.</summary>
    [HttpGet("{runId:guid}/progress")]
    [ProducesResponseType(typeof(StandardBenchmarkRunProgressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StandardBenchmarkRunProgressResponse>> GetProgress(Guid runId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StandardBenchmarkFeatures.GetStandardBenchmarkRunProgressQuery(runId), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToProgressResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Queues a standardized benchmark run for an interaction model. Executes asynchronously.</summary>
    /// <remarks>Requires the Admin role. n_problems of 0 runs the full default dataset; positive values cap problems (per task on task-based suites).</remarks>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(StandardBenchmarkRunResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StandardBenchmarkRunResponse>> Create(
        [FromBody] ExecuteStandardBenchmarkRequest request,
        CancellationToken cancellationToken)
    {
        var command = new StandardBenchmarkFeatures.CreateStandardBenchmarkRunCommand(
            request.InteractionModelId,
            request.BenchmarkKey,
            request.Tasks,
            request.NShots,
            request.NProblems);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }

        return AcceptedAtAction(nameof(GetById), new { runId = result.Value.Id }, result.Value.ToResponse());
    }

    /// <summary>Cancels a queued or running standardized benchmark run.</summary>
    /// <remarks>Requires the Admin role. Cancel is process-level and may take up to one problem to take effect.</remarks>
    [HttpPost("{runId:guid}/cancel")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Cancel(Guid runId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StandardBenchmarkFeatures.CancelStandardBenchmarkRunCommand(runId), cancellationToken);
        return result.IsSuccess
            ? Ok()
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Deletes a standardized benchmark run. Running runs must be cancelled first.</summary>
    /// <remarks>Requires the Admin role. Deletion is hard.</remarks>
    [HttpDelete("{runId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid runId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StandardBenchmarkFeatures.DeleteStandardBenchmarkRunCommand(runId), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Requeues a failed or cancelled run with the same parameters.</summary>
    /// <remarks>Requires the Admin role. The original run row is preserved.</remarks>
    [HttpPost("{runId:guid}/requeue")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(StandardBenchmarkRunResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StandardBenchmarkRunResponse>> Requeue(Guid runId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StandardBenchmarkFeatures.RequeueStandardBenchmarkRunCommand(runId), cancellationToken);
        return result.IsSuccess
            ? AcceptedAtAction(nameof(GetById), new { runId = result.Value.Id }, result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Sets the queue priority of a queued run. Lower values run first.</summary>
    /// <remarks>Requires the Admin role.</remarks>
    [HttpPut("{runId:guid}/priority")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SetPriority(
        Guid runId,
        [FromBody] SetStandardBenchmarkRunPriorityRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new ApiError("StandardBenchmark.InvalidRequest", "A priority value is required."));
        }

        var result = await sender.Send(new StandardBenchmarkFeatures.SetStandardBenchmarkRunPriorityCommand(runId, request.Priority), cancellationToken);
        return result.IsSuccess
            ? Ok()
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Archives a finished standardized benchmark run. Queued or running runs must be cancelled first.</summary>
    /// <remarks>Requires the Admin role. Archived runs are hidden from default lists (HD-4).</remarks>
    [HttpPost("{runId:guid}/archive")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Archive(Guid runId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StandardBenchmarkFeatures.ArchiveStandardBenchmarkRunCommand(runId), cancellationToken);
        return result.IsSuccess
            ? Ok()
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Restores an archived standardized benchmark run to the default list.</summary>
    /// <remarks>Requires the Admin role (HD-4).</remarks>
    [HttpPost("{runId:guid}/unarchive")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Unarchive(Guid runId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StandardBenchmarkFeatures.UnarchiveStandardBenchmarkRunCommand(runId), cancellationToken);
        return result.IsSuccess
            ? Ok()
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Reads the global benchmark queue pause state.</summary>
    /// <remarks>Readable by Auditor and above (HD-8).</remarks>
    [HttpGet("queue/paused")]
    [ProducesResponseType(typeof(BenchmarkQueuePausedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BenchmarkQueuePausedResponse>> GetQueuePaused(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StandardBenchmarkFeatures.GetBenchmarkQueuePausedQuery(), cancellationToken);
        return result.IsSuccess
            ? Ok(new BenchmarkQueuePausedResponse(result.Value))
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Pauses or resumes the global benchmark queue.</summary>
    /// <remarks>Requires the Admin role. A paused queue stops picking up new runs; an executing run finishes (HD-8).</remarks>
    [HttpPut("queue/paused")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BenchmarkQueuePausedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BenchmarkQueuePausedResponse>> SetQueuePaused(
        [FromBody] SetBenchmarkQueuePausedRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new ApiError("StandardBenchmark.InvalidRequest", "A pause value is required."));
        }

        var result = await sender.Send(new StandardBenchmarkFeatures.SetBenchmarkQueuePausedCommand(request.IsPaused), cancellationToken);
        return result.IsSuccess
            ? Ok(new BenchmarkQueuePausedResponse(result.Value))
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Returns the benchmark sidecar health and runner slot state.</summary>
    /// <remarks>Readable by Auditor and above. Reports reachability, transport, slot state, and engine version.</remarks>
    [HttpGet("engine")]
    [ProducesResponseType(typeof(BenchmarkEngineStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BenchmarkEngineStatusResponse>> GetEngineStatus(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StandardBenchmarkFeatures.GetBenchmarkEngineStatusQuery(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Clears a stuck benchmark sidecar runner slot.</summary>
    /// <remarks>Requires the Admin role. Recovery action for a slot held by a dead runner. Safe when the slot is free.</remarks>
    [HttpPost("engine/clear-slot")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BenchmarkEngineSlotClearedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<BenchmarkEngineSlotClearedResponse>> ClearEngineSlot(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StandardBenchmarkFeatures.ClearBenchmarkEngineSlotCommand(), cancellationToken);
        return result.IsSuccess
            ? Ok(new BenchmarkEngineSlotClearedResponse(result.Value))
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Returns the scrubbed runner log for a standardized benchmark run.</summary>
    [HttpGet("{runId:guid}/log")]
    [ProducesResponseType(typeof(StandardBenchmarkRunLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StandardBenchmarkRunLogResponse>> GetLog(Guid runId, CancellationToken cancellationToken)
    {
        var run = await sender.Send(new StandardBenchmarkFeatures.GetStandardBenchmarkRunQuery(runId), cancellationToken);
        if (run.IsFailure)
        {
            return TutoringMapping.ToActionResult(run.Error);
        }

        // G9: the handler synthesizes a diagnostic block for runs that failed
        // before a runner existed (no EngineRunId). Only return 404 when the
        // handler itself reports the run unknown.
        var log = await sender.Send(new StandardBenchmarkFeatures.GetStandardBenchmarkRunLogQuery(runId), cancellationToken);
        return log.IsSuccess
            ? Ok(new StandardBenchmarkRunLogResponse(runId, log.Value ?? string.Empty))
            : TutoringMapping.ToActionResult(log.Error);
    }
}
