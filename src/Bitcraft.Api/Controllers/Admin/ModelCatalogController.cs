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
[Route("api/admin/model-catalog")]
[Produces("application/json")]
public class ModelCatalogController(ISender sender) : ControllerBase
{
    /// <summary>Lists model catalog entries available to administrators.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ModelCatalogEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ModelCatalogEntryResponse>>> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ModelCatalogFeatures.ListModelCatalogEntriesQuery(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(x => x.ToResponse()).ToList())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Creates a model catalog entry for tutor editors to select.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ModelCatalogEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ModelCatalogEntryResponse>> Create(
        [FromBody] CreateModelCatalogEntryRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ModelCatalogFeatures.CreateModelCatalogEntryCommand(
            request.Name,
            request.ProviderType,
            request.Endpoint,
            request.ModelName,
            request.ApiKeySecretRef,
            request.SettingsJson,
            request.IsEnabled);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }

        var response = result.Value.ToResponse();
        return CreatedAtAction(nameof(CheckHealth), new { id = response.Id }, response);
    }

    /// <summary>Checks reachability and model availability for a catalog entry.</summary>
    [HttpGet("{id:guid}/health")]
    [ProducesResponseType(typeof(ModelHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModelHealthResponse>> CheckHealth(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ModelCatalogFeatures.CheckModelCatalogEntryHealthQuery(id), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }
}
