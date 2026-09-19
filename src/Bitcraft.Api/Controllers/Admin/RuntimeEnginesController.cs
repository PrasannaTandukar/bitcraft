using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Admin;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Admin;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Admin;

/// <remarks>
/// Authorization is endpoint-specific: management operations require AdminOnly. The read-only
/// connection test is available to any authenticated user (ViewerOrAbove) per human decision OD-1.
/// </remarks>
[ApiController]
[Route("api/admin/runtime-engines")]
[Produces("application/json")]
public class RuntimeEnginesController(ISender sender) : ControllerBase
{
    /// <summary>Lists admin-managed runtime engines.</summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RuntimeEngineResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RuntimeEngineResponse>>> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RuntimeEngineFeatures.ListRuntimeEnginesQuery(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(x => x.ToResponse()).ToList())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Returns a single runtime engine by id.</summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RuntimeEngineResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RuntimeEngineResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RuntimeEngineFeatures.GetRuntimeEngineQuery(id), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Creates a new runtime engine.</summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost]
    [ProducesResponseType(typeof(RuntimeEngineResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RuntimeEngineResponse>> Create(
        [FromBody] CreateRuntimeEngineRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RuntimeEngineFeatures.CreateRuntimeEngineCommand(
            request.Name,
            request.ProviderType,
            request.Endpoint,
            request.ApiKeySecretRef,
            request.IsEnabled);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }

        var response = result.Value.ToResponse();
        return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
    }

    /// <summary>Updates an existing runtime engine.</summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(RuntimeEngineResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RuntimeEngineResponse>> Update(
        Guid id,
        [FromBody] UpdateRuntimeEngineRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RuntimeEngineFeatures.UpdateRuntimeEngineCommand(
            id,
            request.Name,
            request.ProviderType,
            request.Endpoint,
            request.ApiKeySecretRef,
            request.IsEnabled,
            request.ClearApiKeySecret);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Deletes a runtime engine. Step 4 will introduce FK Restrict from Model rows.</summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RuntimeEngineFeatures.DeleteRuntimeEngineCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Tests connection to a runtime engine. Available to any authenticated user; a stored key is resolved server-side and never returned.</summary>
    [Authorize(Policy = AuthorizationPolicies.ViewerOrAbove)]
    [HttpPost("test")]
    [ProducesResponseType(typeof(TestConnectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TestConnectionResponse>> TestConnection(
        [FromBody] TestRuntimeEngineRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RuntimeEngineFeatures.TestRuntimeEngineCommand(
            request.ProviderType,
            request.Endpoint,
            request.ApiKeySecretRef,
            request.RuntimeEngineId);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Ok(new TestConnectionResponse(
                result.Value.IsSuccess,
                result.Value.LatencyMs,
                result.Value.Message,
                result.Value.AvailableModels))
            : TutoringMapping.ToActionResult(result.Error);
    }
}
