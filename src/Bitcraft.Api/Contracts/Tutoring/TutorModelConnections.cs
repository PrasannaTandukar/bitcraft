namespace Bitcraft.Api.Contracts.Tutoring;

/// <summary>Request to bind a tutor to an Admin-managed model catalog entry.</summary>
public sealed record SaveTutorModelConnectionRequest(
    Guid ModelCatalogEntryId,
    string? OverrideSettingsJson);

/// <summary>Per-tutor model selection and optional override settings.</summary>
public sealed record TutorModelConnectionResponse(
    Guid Id,
    Guid TutorId,
    Guid ModelCatalogEntryId,
    string? OverrideSettingsJson,
    DateTime CreatedUtc,
    DateTime? LastModifiedUtc);
