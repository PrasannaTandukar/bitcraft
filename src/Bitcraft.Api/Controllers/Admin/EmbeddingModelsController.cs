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
[Route("api/admin/embedding-models")]
[Produces("application/json")]
public class EmbeddingModelsController(ISender sender) : ControllerBase
{
    /// <summary>Lists admin-managed embedding models.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmbeddingModelResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<EmbeddingModelResponse>>> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new EmbeddingModelFeatures.ListEmbeddingModelsQuery(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(x => x.ToResponse()).ToList())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Returns a single embedding model by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmbeddingModelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmbeddingModelResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new EmbeddingModelFeatures.GetEmbeddingModelQuery(id), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Creates a new embedding model. <c>EmbeddingDimensions</c> is immutable after creation.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmbeddingModelResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EmbeddingModelResponse>> Create(
        [FromBody] CreateEmbeddingModelRequest request,
        CancellationToken cancellationToken)
    {
        var command = new EmbeddingModelFeatures.CreateEmbeddingModelCommand(
            request.Name,
            request.RuntimeEngineId,
            request.ProviderModelName,
            request.EmbeddingDimensions,
            request.SettingsJson,
            request.IsEnabled);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }

        var response = result.Value.ToResponse();
        return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
    }

    /// <summary>Updates an existing embedding model. The stored <c>EmbeddingDimensions</c> must match the request value.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmbeddingModelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmbeddingModelResponse>> Update(
        Guid id,
        [FromBody] UpdateEmbeddingModelRequest request,
        CancellationToken cancellationToken)
    {
        var command = new EmbeddingModelFeatures.UpdateEmbeddingModelCommand(
            id,
            request.Name,
            request.RuntimeEngineId,
            request.ProviderModelName,
            request.EmbeddingDimensions,
            request.SettingsJson,
            request.IsEnabled);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Deletes an embedding model.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new EmbeddingModelFeatures.DeleteEmbeddingModelCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : TutoringMapping.ToActionResult(result.Error);
    }
}
