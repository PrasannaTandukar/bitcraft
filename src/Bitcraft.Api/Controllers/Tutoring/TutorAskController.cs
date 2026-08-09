using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Tutoring;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Tutoring;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ViewerOrAbove)]
[Route("api/tutor")]
[Produces("application/json")]
public class TutorAskController(ISender sender) : ControllerBase
{
    /// <summary>Answers a question, optionally using and saving tutor memory.</summary>
    [HttpPost("ask")]
    [ProducesResponseType(typeof(AskTutorQuestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<AskTutorQuestionResponse>> Ask(
        [FromBody] AskTutorQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TutorAskFeatures.AskTutorQuestionCommand(
                request.Question,
                request.Model,
                request.UseMemory,
                request.SaveToMemory,
                request.TopK,
                request.TutorId,
                request.History?.Select(message => message is null
                    ? null!
                    : new Bitcraft.Application.Common.Models.TutorRuntimeConversationMessage(
                        message.Role,
                        message.Content)).ToList()),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }
}
