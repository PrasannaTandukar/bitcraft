using System.Text.Json;
using System.Text.RegularExpressions;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>
/// Tool definition use-cases. JSON schemas are validated at intake to fail
/// fast on malformed payloads before they reach an LLM agent runtime.
/// </summary>
public static partial class ToolDefinitionFeatures
{
    public sealed record RegisterToolDefinitionCommand(
        Guid TutorId,
        string Name,
        string? Description,
        string JsonSchema,
        TutorToolKind Kind = TutorToolKind.BuiltIn,
        string? ImplementationKey = null,
        TutorToolExecutionPolicy ExecutionPolicy = TutorToolExecutionPolicy.Disabled) : IRequest<Result<ToolDefinition>>;

    public sealed class RegisterToolDefinitionHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser,
        ITutorToolCatalog catalog)
        : IRequestHandler<RegisterToolDefinitionCommand, Result<ToolDefinition>>
    {
        public async Task<Result<ToolDefinition>> Handle(RegisterToolDefinitionCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 64 ||
                !ToolNamePattern().IsMatch(request.Name.Trim()))
            {
                return Result.Failure<ToolDefinition>(new Error("Tool.NameInvalid", "Tool name must be 1-64 letters, digits, underscores, or hyphens and start with a letter or underscore."));
            }

            if (request.Description?.Length > 2000)
                return Result.Failure<ToolDefinition>(new Error("Tool.DescriptionInvalid", "Description must be at most 2000 characters."));

            if (string.IsNullOrWhiteSpace(request.JsonSchema))
            {
                return Result.Failure<ToolDefinition>(new Error("Tool.SchemaRequired", "Tool JSON schema is required."));
            }

            if (request.JsonSchema.Length > 16_384)
                return Result.Failure<ToolDefinition>(new Error("Tool.SchemaInvalid", "Tool JSON schema is too large."));

            try
            {
                using var schema = JsonDocument.Parse(request.JsonSchema, new JsonDocumentOptions { MaxDepth = 16 });
                if (schema.RootElement.ValueKind != JsonValueKind.Object ||
                    !schema.RootElement.TryGetProperty("type", out var type) || (type.ValueKind != JsonValueKind.String || type.GetString() != "object") ||
                    schema.RootElement.TryGetProperty("properties", out var properties) && properties.ValueKind != JsonValueKind.Object ||
                    ContainsReference(schema.RootElement))
                    return Result.Failure<ToolDefinition>(new Error("Tool.SchemaInvalid", "Tool schema must be a bounded object schema without references."));
            }
            catch (JsonException)
            {
                return Result.Failure<ToolDefinition>(new Error("Tool.SchemaInvalid", "Tool JSON schema is not valid JSON."));
            }

            if (!Enum.IsDefined(request.Kind) || !Enum.IsDefined(request.ExecutionPolicy))
                return Result.Failure<ToolDefinition>(new Error("Tool.ConfigurationInvalid", "Tool kind or execution policy is invalid."));
            var implementationKey = request.ImplementationKey?.Trim() ?? string.Empty;
            if (implementationKey.Length is < 1 or > 100 || !ImplementationKeyPattern().IsMatch(implementationKey))
                return Result.Failure<ToolDefinition>(new Error("Tool.ImplementationInvalid", "Select a registered tool implementation."));
            if (request.Kind is TutorToolKind.Mcp or TutorToolKind.ApprovedHttp)
                return Result.Failure<ToolDefinition>(new Error("Tool.KindUnavailable", "This tutor tool kind is not enabled."));

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

            var implementation = catalog.Entries.FirstOrDefault(entry => entry.ImplementationKey == implementationKey && entry.Kind == request.Kind);
            if (implementation is null)
                return Result.Failure<ToolDefinition>(new Error("Tool.ImplementationInvalid", "Select a registered tool implementation."));
            if (request.ExecutionPolicy == TutorToolExecutionPolicy.ReadOnlyAutomatic && !implementation.SupportsAutomaticExecution)
                return Result.Failure<ToolDefinition>(new Error("Tool.ApprovalRequired", "This tool cannot execute automatically."));
            if ((await tutorRepository.ListToolDefinitionsAsync(tutor.Id, cancellationToken)).Any(tool => tool.Name.Equals(request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                return Result.Failure<ToolDefinition>(new Error("Tool.NameDuplicate", "A tool with this name already exists for this tutor."));

            var tool = new ToolDefinition
            {
                TutorId = request.TutorId,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                JsonSchema = implementation.JsonSchema,
                Kind = request.Kind,
                ImplementationKey = implementationKey,
                ExecutionPolicy = request.ExecutionPolicy
            };

            var created = await tutorRepository.AddToolDefinitionAsync(tool, cancellationToken);
            return Result.Success(created);
        }
    }

    private static bool ContainsReference(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("$ref")) return true;
                if (ContainsReference(property.Value)) return true;
            }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray())
                if (ContainsReference(child)) return true;
        return false;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolNamePattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex ImplementationKeyPattern();
}
