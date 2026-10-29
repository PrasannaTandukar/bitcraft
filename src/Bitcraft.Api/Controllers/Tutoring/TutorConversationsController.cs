using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Tutoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Response = Bitcraft.Api.Contracts.Tutoring.TutorConversationResponse;

namespace Bitcraft.Api.Controllers.Tutoring;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
[Route("api/runtime/tutors/{activationSlug}/conversations")]
public sealed class TutorConversationsController(TutorConversationService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Response>> Create(string activationSlug, CancellationToken token)
    {
        var result = await service.CreateAsync(activationSlug, token);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { activationSlug, conversationId = result.Value.Id }, Map(result.Value))
            : TutoringMapping.ToActionResult(result.Error);
    }
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Response>>> List(string activationSlug, CancellationToken token)
    {
        var result = await service.ListAsync(activationSlug, token);
        return result.IsSuccess ? Ok(result.Value.Select(Map)) : TutoringMapping.ToActionResult(result.Error);
    }
    [HttpGet("{conversationId:guid}")]
    public async Task<ActionResult<Response>> Get(string activationSlug, Guid conversationId, CancellationToken token)
    {
        var result = await service.GetAsync(activationSlug, conversationId, token);
        return result.IsSuccess ? Ok(Map(result.Value)) : TutoringMapping.ToActionResult(result.Error);
    }
    [HttpDelete("{conversationId:guid}")]
    public async Task<IActionResult> Delete(string activationSlug, Guid conversationId, CancellationToken token)
    {
        var result = await service.DeleteAsync(activationSlug, conversationId, token);
        return result.IsSuccess ? NoContent() : TutoringMapping.ToActionResult(result.Error);
    }
    private static Response Map(Bitcraft.Application.Features.Tutoring.TutorConversationResponse value) => new(
        value.Id, value.TutorId, value.TutorRevisionId, value.SnapshotId, value.Title, value.CreatedUtc, value.UpdatedUtc,
        value.Messages.Select(message => new TutorConversationStoredMessageResponse(message.Id, message.Sequence,
            message.Role, message.Content, message.CreatedUtc, message.AttachmentIds, message.ProviderModelName, message.ProviderType)
            { Provenance = message.Provenance?.ToResponse() }).ToList(), value.RevisionNumber, value.SupportsImageInput);
}
