using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Api.Contracts.Tutoring;

/// <summary>Request to create or replace the tutor deployment profile.</summary>
public sealed record SaveDeploymentProfileRequest(
    DeploymentTarget Target,
    string? ConfigurationJson);

/// <summary>Deployment target and configuration currently assigned to a tutor.</summary>
public sealed record DeploymentProfileResponse(
    Guid Id,
    Guid TutorId,
    DeploymentTarget Target,
    string? ConfigurationJson,
    DateTime CreatedUtc,
    DateTime? LastModifiedUtc);
