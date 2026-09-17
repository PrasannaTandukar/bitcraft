using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Tutoring;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Tutoring;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
[Route("api/runtime/tutors")]
[Produces("application/json")]
public sealed class TutorRuntimeController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TutorRuntimeDescriptorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<TutorRuntimeDescriptorResponse>>> List(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TutorRuntimeFeatures.ListRuntimeTutorsQuery(), cancellationToken);
        return Ok(result.Select(value => value.ToResponse()).ToList());
    }

    [HttpPost("{activationSlug}/ask")]
    [ProducesResponseType(typeof(TutorRuntimeAskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<TutorRuntimeAskResponse>> Ask(
        string activationSlug,
        [FromBody] AskTutorRuntimeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ConversationId.HasValue)
        {
            return BadRequest(new ApiError(
                "TutorRuntime.ConversationPersistenceUnavailable",
                "Persistent conversations are not available yet. Send bounded history with the request instead."));
        }

        var history = request.History?.Select(message =>
            message is null
                ? null!
                : new TutorRuntimeConversationMessage(message.Role, message.Content, message.AttachmentIds)).ToList();
        var result = await sender.Send(
            new TutorRuntimeFeatures.AskTutorRuntimeCommand(
                activationSlug,
                request.Question,
                request.TopK,
                history,
                request.AttachmentIds),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }
}
