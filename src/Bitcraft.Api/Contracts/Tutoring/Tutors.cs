using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Api.Contracts.Tutoring;

/// <summary>Request to create a draft tutor owned by the authenticated user.</summary>
public sealed record CreateTutorRequest(string Name, string? Description);

/// <summary>Request to update tutor metadata, lifecycle status, and active prompt selection.</summary>
public sealed record UpdateTutorRequest(
    string Name,
    string? Description,
    TutorStatus Status,
    Guid? ActivePromptVersionId);

/// <summary>Top-level tutor aggregate response for Tutor Studio management screens.</summary>
public sealed record TutorResponse(
    Guid Id,
    Guid OwnerId,
    string Name,
    string? Description,
    TutorStatus Status,
    Guid? TutorModelConnectionId,
    Guid? ActivePromptVersionId,
    Guid? DeploymentProfileId,
    DateTime CreatedUtc,
    DateTime? LastModifiedUtc);
