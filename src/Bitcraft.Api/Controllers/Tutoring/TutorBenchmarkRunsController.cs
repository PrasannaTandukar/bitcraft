using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Benchmarking;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Tutoring;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
[Route("api/tutors/{tutorId:guid}/benchmark-runs")]
[Produces("application/json")]
public class TutorBenchmarkRunsController(ISender sender) : ControllerBase
{
    /// <summary>Lists benchmark runs for a tutor owned by the authenticated user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TutorBenchmarkRunSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TutorBenchmarkRunSummaryResponse>>> List(Guid tutorId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new BenchmarkFeatures.ListTutorBenchmarkRunsQuery(tutorId), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(x => x.ToSummaryResponse()).ToList())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Gets a benchmark run detail including case-level evaluation output.</summary>
    [HttpGet("{runId:guid}")]
    [ProducesResponseType(typeof(TutorBenchmarkRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorBenchmarkRunResponse>> GetById(Guid tutorId, Guid runId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new BenchmarkFeatures.GetTutorBenchmarkRunQuery(tutorId, runId), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Gets lightweight benchmark progress for polling while a run is queued or executing.</summary>
    [HttpGet("{runId:guid}/progress")]
    [ProducesResponseType(typeof(TutorBenchmarkRunProgressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorBenchmarkRunProgressResponse>> GetProgress(Guid tutorId, Guid runId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new BenchmarkFeatures.GetTutorBenchmarkRunProgressQuery(tutorId, runId), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToProgressResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Queues a new benchmark run that will execute asynchronously.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(TutorBenchmarkRunSummaryResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorBenchmarkRunSummaryResponse>> Create(
        Guid tutorId,
        [FromBody] CreateTutorBenchmarkRunRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.Cases is null || request.Cases.Count == 0)
        {
            return BadRequest(new ApiError("Benchmark.CasesRequired", "At least one benchmark case is required."));
        }

        if (request.Cases.Any(x => x is null))
        {
            return BadRequest(new ApiError("Benchmark.CaseInvalid", "Each benchmark case must be a valid object."));
        }

        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            tutorId,
            request.BenchmarkTemplateId,
            request.TutorConfigurationSnapshotId,
            request.ModelCatalogEntryId,
            request.ModelAlias,
            request.ConsistencyRuns,
            request.Cases.Select(x => new BenchmarkFeatures.BenchmarkCaseInput(
                x!.InputText,
                x.ActualOutput,
                x.ExpectedOutput,
                x.ContextText,
                x.MetadataJson)).ToList());

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }

        var response = result.Value.ToSummaryResponse();
        return AcceptedAtAction(nameof(GetById), new { tutorId, runId = response.Id }, response);
    }
}
