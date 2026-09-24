using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Api.Contracts.Tutoring;

/// <summary>Request to register an executable tool schema for a tutor.</summary>
public sealed record RegisterToolDefinitionRequest(
    string Name,
    string? Description,
    string JsonSchema = "{\"type\":\"object\"}",
    TutorToolKind Kind = TutorToolKind.BuiltIn,
    string? ImplementationKey = null,
    TutorToolExecutionPolicy ExecutionPolicy = TutorToolExecutionPolicy.Disabled);

/// <summary>Tool definition available to a tutor runtime.</summary>
public sealed record ToolDefinitionResponse(
    Guid Id,
    Guid TutorId,
    string Name,
    string? Description,
    string JsonSchema,
    DateTime CreatedUtc,
    TutorToolKind Kind,
    string ImplementationKey,
    TutorToolExecutionPolicy ExecutionPolicy);

public sealed record UpdateToolDefinitionRequest(string Name, string? Description, TutorToolKind Kind,
    string ImplementationKey, TutorToolExecutionPolicy ExecutionPolicy);

public sealed record TutorToolCatalogResponse(string ImplementationKey, string Name, string Description,
    TutorToolKind Kind, string JsonSchema, bool SupportsAutomaticExecution);
