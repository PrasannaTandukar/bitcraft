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

    public sealed class RegisterToolDefinitionHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
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

            // D2/D3: tool definitions mutate tutor config → Editor or Owner.
            var role = await membership.GetRoleAsync(tutor.Id, currentUser.UserId, cancellationToken);
            if (role is not ResourceRole.Owner and not ResourceRole.Editor)
            {
                return Result.Failure<ToolDefinition>(new Error("Tutor.Forbidden", "Tutor membership required."));
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
