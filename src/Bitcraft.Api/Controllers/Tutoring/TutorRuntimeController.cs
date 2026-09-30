using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Tutoring;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Bitcraft.Api.Controllers.Tutoring;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
[Route("api/runtime/tutors")]
[Produces("application/json")]
public sealed class TutorRuntimeController(ISender sender) : ControllerBase
{
    private const int MaximumAskRequestBytes = 256 * 1024;
    private static readonly JsonSerializerOptions StreamJson = new(JsonSerializerDefaults.Web);
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
    [RequestSizeLimit(MaximumAskRequestBytes)]
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
        var result = await sender.Send(
            new TutorRuntimeFeatures.AskTutorRuntimeCommand(
                activationSlug,
                request.Question,
                request.TopK,
                request.AttachmentIds, request.ConversationId,
                request.UseMemory, request.SaveToMemory),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    [HttpPost("{activationSlug}/ask/stream")]
    [RequestSizeLimit(MaximumAskRequestBytes)]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task Stream(string activationSlug, [FromBody] AskTutorRuntimeRequest request,
        CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers.Append("X-Accel-Buffering", "no");
        await foreach (var value in sender.CreateStream(new TutorRuntimeFeatures.StreamTutorRuntimeCommand(
            activationSlug, request.Question, request.TopK, request.AttachmentIds,
            request.ConversationId, request.UseMemory, request.SaveToMemory), cancellationToken))
        {
            var response = value.ToResponse();
            await Response.WriteAsync($"id: {response.Sequence}\n", cancellationToken);
            await Response.WriteAsync($"event: {response.Type}\n", cancellationToken);
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(response, StreamJson)}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
