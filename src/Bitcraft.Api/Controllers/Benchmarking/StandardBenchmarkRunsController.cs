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
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StandardBenchmarkRunResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<StandardBenchmarkRunResponse>>> List(
        [FromQuery] Guid? interactionModelId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new StandardBenchmarkFeatures.ListStandardBenchmarkRunsQuery(interactionModelId, limit), cancellationToken);
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
}
