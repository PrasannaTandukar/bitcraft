using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Ollama;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Ollama;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Ollama;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ViewerOrAbove)]
[Route("api/ollama")]
[Produces("application/json")]
public class OllamaController(ISender sender) : ControllerBase
{
    /// <summary>Generates text using the configured local Ollama chat model.</summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GenerateOllamaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GenerateOllamaResponse>> Generate(
        [FromBody] GenerateOllamaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new OllamaGenerationFeatures.GenerateTextCommand(request.Prompt, request.Model),
            cancellationToken);

        return result.IsSuccess
            ? Ok(new GenerateOllamaResponse(result.Value.Model, result.Value.Response, result.Value.Done))
            : TutoringMapping.ToActionResult(result.Error);
    }
}
