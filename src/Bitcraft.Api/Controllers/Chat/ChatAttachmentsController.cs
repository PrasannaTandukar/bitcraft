using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Chat;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ChatAttachmentResponse = Bitcraft.Api.Contracts.Chat.ChatAttachmentResponse;

namespace Bitcraft.Api.Controllers.Chat;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AuditorOrAbove)]
[Route("api/runtime/attachments")]
public sealed class ChatAttachmentsController(
    IChatAttachmentService attachments,
    ICurrentUserContext currentUser,
    ChatAttachmentLimits limits) : ControllerBase
{
    [HttpGet("options")]
    public ActionResult<ChatAttachmentOptionsResponse> Options() => Ok(new ChatAttachmentOptionsResponse(
        limits.MaxFileBytes, limits.MaxImagesPerMessage, limits.MaxImagesPerRequest,
        limits.MaxDimension, limits.RetentionHours, ["image/jpeg", "image/png", "image/webp"]));

    /// <summary>Uploads one image as multipart/form-data with a single file field named 'file'.</summary>
    [HttpPost("images")]
    [Consumes("multipart/form-data")]
    [ChatImageUploadLimit]
    [ProducesResponseType(typeof(ChatAttachmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<ChatAttachmentResponse>> Upload([FromForm] UploadChatImageRequest request, CancellationToken cancellationToken)
    {
        await using var input = request.File.OpenReadStream();
        var result = await attachments.UploadAsync(currentUser.UserId, request.File.FileName, request.File.ContentType, input, cancellationToken);
        if (result.IsFailure) return TutoringMapping.ToActionResult(result.Error);
        var value = result.Value;
        var response = new ChatAttachmentResponse(value.Id, value.FileName, value.MimeType, value.ByteLength, value.Width, value.Height, value.ExpiresUtc);
        return CreatedAtAction(nameof(Get), new { id = value.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status410Gone)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await attachments.ReadAsync(id, currentUser.UserId, cancellationToken);
        if (result.IsFailure) return TutoringMapping.ToActionResult(result.Error);
        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(result.Value.Data.ToArray(), result.Value.Metadata.MimeType);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await attachments.DeleteAsync(id, currentUser.UserId, cancellationToken);
        return result.IsSuccess ? NoContent() : TutoringMapping.ToActionResult(result.Error);
    }
}

/// <summary>Bound multipart parsing before model binding, including requests without Content-Length.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ChatImageUploadLimitAttribute : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var limits = http.RequestServices.GetRequiredService<ChatAttachmentLimits>();
        var bodyLimit = limits.MaxFileBytes + 64 * 1024;
        if (http.Request.ContentLength > bodyLimit)
        {
            context.Result = TooLarge();
            return;
        }
        var feature = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = bodyLimit;
        try
        {
            var form = await http.Request.ReadFormAsync(new FormOptions
            {
                MultipartBodyLengthLimit = bodyLimit,
                ValueCountLimit = 4,
                ValueLengthLimit = 1024
            }, http.RequestAborted);
            if (form.Files.Count != 1 || form.Files[0].Name != "file")
            {
                context.Result = new BadRequestObjectResult(new ApiError("ChatAttachment.InvalidUpload", "Upload exactly one image in the file field."));
                return;
            }
            if (form.Files[0].Length > limits.MaxFileBytes)
            {
                context.Result = TooLarge();
                return;
            }
        }
        catch (InvalidDataException)
        {
            context.Result = TooLarge();
            return;
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            context.Result = TooLarge();
            return;
        }
        await next();
    }

    private static ObjectResult TooLarge() => new(new ApiError("ChatAttachment.FileTooLarge", "The image upload exceeds the configured size limit."))
        { StatusCode = StatusCodes.Status413PayloadTooLarge };
}
