using System.Text.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>
/// Tool definition use-cases. JSON schemas are validated at intake to fail
/// fast on malformed payloads before they reach an LLM agent runtime.
/// </summary>
public static class ToolDefinitionFeatures
{
    public sealed record RegisterToolDefinitionCommand(
        Guid TutorId,
        string Name,
        string? Description,
        string JsonSchema,
        string? ExecutionUrl) : IRequest<Result<ToolDefinition>>;

    public sealed class RegisterToolDefinitionHandler(ITutorRepository tutorRepository, ICurrentUserContext currentUser)
        : IRequestHandler<RegisterToolDefinitionCommand, Result<ToolDefinition>>
    {
        public async Task<Result<ToolDefinition>> Handle(RegisterToolDefinitionCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Result.Failure<ToolDefinition>(new Error("Tool.NameRequired", "Tool name is required."));
            }

            if (string.IsNullOrWhiteSpace(request.JsonSchema))
            {
                return Result.Failure<ToolDefinition>(new Error("Tool.SchemaRequired", "Tool JSON schema is required."));
            }

            try
            {
                using var _ = JsonDocument.Parse(request.JsonSchema);
            }
            catch (JsonException)
            {
                return Result.Failure<ToolDefinition>(new Error("Tool.SchemaInvalid", "Tool JSON schema is not valid JSON."));
            }

            var tutor = await tutorRepository.GetByIdAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<ToolDefinition>(new Error("Tutor.NotFound", $"Tutor '{request.TutorId}' not found."));
            }

            if (tutor.OwnerId != currentUser.UserId)
            {
                return Result.Failure<ToolDefinition>(new Error("Tutor.Forbidden", "Requesting user does not own this tutor."));
            }

            var tool = new ToolDefinition
            {
                TutorId = request.TutorId,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                JsonSchema = request.JsonSchema,
                ExecutionUrl = request.ExecutionUrl?.Trim()
            };

            var created = await tutorRepository.AddToolDefinitionAsync(tool, cancellationToken);
            return Result.Success(created);
        }
    }
}
