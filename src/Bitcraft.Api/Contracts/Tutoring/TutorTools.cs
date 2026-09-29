namespace Bitcraft.Api.Contracts.Tutoring;

/// <summary>Request to register an executable tool schema for a tutor.</summary>
public sealed record RegisterToolDefinitionRequest(
    string Name,
    string? Description,
    string JsonSchema,
    string? ExecutionUrl);

/// <summary>Tool definition available to a tutor runtime.</summary>
public sealed record ToolDefinitionResponse(
    Guid Id,
    Guid TutorId,
    string Name,
    string? Description,
    string JsonSchema,
    string? ExecutionUrl,
    DateTime CreatedUtc);