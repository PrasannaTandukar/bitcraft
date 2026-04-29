namespace Bitcraft.Api.Contracts.Tutoring;

/// <summary>Request to append a new prompt version to a tutor.</summary>
public sealed record CreatePromptVersionRequest(
    string Name,
    string Content,
    string? Notes,
    bool SetAsActive);

/// <summary>Immutable prompt version assigned to a tutor.</summary>
public sealed record PromptVersionResponse(
    Guid Id,
    Guid TutorId,
    int VersionNumber,
    string Name,
    string Content,
    string? Notes,
    DateTime CreatedUtc);