namespace Bitcraft.Api.Contracts.Tutoring;

/// <summary>Request to bind a tutor to an admin-managed <c>InteractionModel</c>.</summary>
public sealed record SaveTutorModelConnectionRequest(
    Guid InteractionModelId,
    string? OverrideSettingsJson);

/// <summary>Per-tutor model selection and optional override settings.</summary>
public sealed record TutorModelConnectionResponse(
    Guid Id,
    Guid TutorId,
    Guid InteractionModelId,
    string? OverrideSettingsJson,
    DateTime CreatedUtc,
    DateTime? ModifiedUtc);
