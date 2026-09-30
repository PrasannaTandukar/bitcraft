using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Features.Tutoring;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Tutoring;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.EditorOrAdmin)]
[Route("api/tutors/{tutorId:guid}/tools")]
[Produces("application/json")]
public class TutorToolsController(ISender sender, ITutorToolCatalog catalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ToolDefinitionResponse>>> List(Guid tutorId, CancellationToken ct)
    {
        var result = await sender.Send(new TutorToolManagementFeatures.ListToolsQuery(tutorId), ct);
        if (result.IsFailure) return TutoringMapping.ToActionResult(result.Error);
        return Ok(result.Value.Select(tool => tool.ToResponse()).ToArray());
    }

    [HttpGet("catalog")]
    public async Task<ActionResult<IReadOnlyList<TutorToolCatalogResponse>>> Catalog(Guid tutorId, CancellationToken ct)
    {
        var result = await sender.Send(new TutorToolManagementFeatures.ListToolsQuery(tutorId), ct);
        if (result.IsFailure) return TutoringMapping.ToActionResult(result.Error);
        return Ok(catalog.Entries.Select(entry => new TutorToolCatalogResponse(entry.ImplementationKey,
            entry.Name, entry.Description, entry.Kind, entry.JsonSchema, entry.SupportsAutomaticExecution)).ToArray());
    }

    [HttpGet("{toolId:guid}")]
    public async Task<ActionResult<ToolDefinitionResponse>> GetById(Guid tutorId, Guid toolId, CancellationToken ct)
    {
        var result = await sender.Send(new TutorToolManagementFeatures.ListToolsQuery(tutorId), ct);
        if (result.IsFailure) return TutoringMapping.ToActionResult(result.Error);
        var tool = result.Value.FirstOrDefault(tool => tool.Id == toolId);
        return tool is null ? NotFound(new ApiError("Tool.NotFound", "Tool not found.")) : Ok(tool.ToResponse());
    }

    [HttpPut("{toolId:guid}")]
    public async Task<ActionResult<ToolDefinitionResponse>> Update(Guid tutorId, Guid toolId,
        UpdateToolDefinitionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new TutorToolManagementFeatures.UpdateToolCommand(tutorId, toolId,
            request.Name, request.Description, request.Kind, request.ImplementationKey, request.ExecutionPolicy), ct);
        if (result.IsFailure) return TutoringMapping.ToActionResult(result.Error);
        return Ok(result.Value.ToResponse());
    }

    [HttpDelete("{toolId:guid}")]
    public async Task<IActionResult> Delete(Guid tutorId, Guid toolId, CancellationToken ct)
    {
        var result = await sender.Send(new TutorToolManagementFeatures.DeleteToolCommand(tutorId, toolId), ct);
        if (result.IsFailure) return TutoringMapping.ToActionResult(result.Error);
        return NoContent();
    }

    /// <summary>Registers a tool definition for a tutor owned by the authenticated user.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ToolDefinitionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ToolDefinitionResponse>> Register(
        Guid tutorId,
        [FromBody] RegisterToolDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ToolDefinitionFeatures.RegisterToolDefinitionCommand(
            tutorId,
            request.Name,
            request.Description,
            request.JsonSchema,
            request.Kind,
            request.ImplementationKey,
            request.ExecutionPolicy);
        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return TutoringMapping.ToActionResult(result.Error);
        }

        return CreatedAtAction(nameof(GetById), new { tutorId, toolId = result.Value.Id }, result.Value.ToResponse());
    }
}
