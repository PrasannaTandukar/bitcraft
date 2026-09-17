using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Admin;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Admin;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Admin;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/admin/interaction-models")]
[Produces("application/json")]
public class InteractionModelsController(ISender sender) : ControllerBase
{
    /// <summary>Lists admin-managed interaction models.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InteractionModelResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<InteractionModelResponse>>> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new InteractionModelFeatures.ListInteractionModelsQuery(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(x => x.ToResponse()).ToList())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Returns a single interaction model by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InteractionModelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InteractionModelResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new InteractionModelFeatures.GetInteractionModelQuery(id), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Creates a new interaction model bound to a runtime engine.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(InteractionModelResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InteractionModelResponse>> Create(
        [FromBody] CreateInteractionModelRequest request,
        CancellationToken cancellationToken)
    {
        var command = new InteractionModelFeatures.CreateInteractionModelCommand(
            request.Name,
            request.RuntimeEngineId,
            request.ProviderModelName,
            request.SettingsJson,
            request.IsEnabled,
            request.SupportsImageInput);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }

        var response = result.Value.ToResponse();
        return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
    }

    /// <summary>Updates an existing interaction model.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(InteractionModelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InteractionModelResponse>> Update(
        Guid id,
        [FromBody] UpdateInteractionModelRequest request,
        CancellationToken cancellationToken)
    {
        var command = new InteractionModelFeatures.UpdateInteractionModelCommand(
            id,
            request.Name,
            request.RuntimeEngineId,
            request.ProviderModelName,
            request.SettingsJson,
            request.IsEnabled,
            request.SupportsImageInput);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Tests image input on the saved model. Does not modify configured capabilities.</summary>
    [HttpPost("{id:guid}/test-image")]
    [ProducesResponseType(typeof(TestInteractionModelImageResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TestInteractionModelImageResponse>> TestImage(
        Guid id, [FromBody] TestInteractionModelImageRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new InteractionModelFeatures.TestImageInputCommand(id, request.AttachmentId), cancellationToken);
        return result.IsSuccess
            ? Ok(new TestInteractionModelImageResponse(result.Value.Answer))
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Deletes an interaction model. FK Restrict applies from tutor connections / snapshots / benchmark runs.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new InteractionModelFeatures.DeleteInteractionModelCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : TutoringMapping.ToActionResult(result.Error);
    }
}
