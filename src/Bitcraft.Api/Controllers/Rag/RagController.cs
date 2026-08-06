using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Rag;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Rag;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Rag;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ViewerOrAbove)]
[Route("api/rag")]
[Produces("application/json")]
public class RagController(ISender sender) : ControllerBase
{
    /// <summary>Answers a question using uploaded document chunks owned by the authenticated user.</summary>
    [HttpPost("ask")]
    [ProducesResponseType(typeof(AskRagQuestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AskRagQuestionResponse>> Ask(
        [FromBody] AskRagQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RagAnswerFeatures.AskRagQuestionCommand(
                request.Question,
                request.Model,
                request.TopK),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }
}
