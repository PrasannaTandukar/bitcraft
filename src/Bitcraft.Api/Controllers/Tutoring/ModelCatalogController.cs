using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Tutoring;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Tutoring;

/// <summary>
/// Read-only model selectors for tutor configuration. Global model mutation remains available
/// only through the Admin controllers.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
[Route("api/model-catalog")]
[Produces("application/json")]
public sealed class ModelCatalogController(ISender sender) : ControllerBase
{
    [HttpGet("interaction-models")]
    [ProducesResponseType(typeof(IReadOnlyList<TutorInteractionModelCatalogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TutorInteractionModelCatalogResponse>>> ListInteractionModels(
        [FromQuery] Guid tutorId,
        [FromQuery] bool includeDisabled,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ModelCatalogFeatures.ListInteractionModelsQuery(tutorId, includeDisabled),
            cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(x => x.ToCatalogResponse()).ToList())
            : TutoringMapping.ToActionResult(result.Error);
    }

    [HttpGet("embedding-models")]
    [ProducesResponseType(typeof(IReadOnlyList<TutorEmbeddingModelCatalogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TutorEmbeddingModelCatalogResponse>>> ListEmbeddingModels(
        [FromQuery] Guid? tutorId,
        [FromQuery] bool includeDisabled,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ModelCatalogFeatures.ListEmbeddingModelsQuery(tutorId, includeDisabled),
            cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(x => x.ToCatalogResponse()).ToList())
            : TutoringMapping.ToActionResult(result.Error);
    }
}
