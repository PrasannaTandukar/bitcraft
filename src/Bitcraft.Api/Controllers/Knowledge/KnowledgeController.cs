using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Knowledge;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Knowledge;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Knowledge;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
[Route("api/knowledge")]
[Produces("application/json")]
public class KnowledgeController(ISender sender) : ControllerBase
{
    /// <summary>Searches knowledge chunks linked to a tutor owned by the authenticated user.</summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(IReadOnlyList<KnowledgeChunkResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<KnowledgeChunkResponse>>> Search(
        [FromBody] SearchKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new KnowledgeFeatures.SearchKnowledgeQuery(request.TutorId, request.QueryEmbedding, request.TopK), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(x => x.ToResponse()).ToList())
            : TutoringMapping.ToActionResult(result.Error);
    }
}
