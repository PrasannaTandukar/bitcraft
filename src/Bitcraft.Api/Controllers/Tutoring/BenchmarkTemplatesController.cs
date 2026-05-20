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
[Route("api/benchmark/templates")]
[Produces("application/json")]
public class BenchmarkTemplatesController(ISender sender) : ControllerBase
{
    /// <summary>Lists benchmark templates owned by the authenticated editor.</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(IReadOnlyList<BenchmarkTemplateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<BenchmarkTemplateResponse>>> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new BenchmarkFeatures.ListBenchmarkTemplatesQuery(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(x => x.ToResponse()).ToList())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Creates a benchmark template owned by the authenticated user.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(typeof(BenchmarkTemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BenchmarkTemplateResponse>> Create([FromBody] CreateBenchmarkTemplateRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new ApiError("Benchmark.TemplateInvalid", "Benchmark template request is required."));
        }

        var command = new BenchmarkFeatures.CreateBenchmarkTemplateCommand(
            request.Key,
            request.Name,
            request.Description,
            request.GuidancePath,
            request.MetricsJson,
            request.IsEnabled);
        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }

        var response = result.Value.ToResponse();
        return CreatedAtAction(nameof(List), null, response);
    }

    /// <summary>Deletes an owned benchmark template when it is not referenced by any run.</summary>
    [HttpDelete("{templateId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid templateId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new BenchmarkFeatures.DeleteBenchmarkTemplateCommand(templateId), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : TutoringMapping.ToActionResult(result.Error);
    }
}
