namespace Bitcraft.Application.Common.Models;

/// <summary>Why a tutor can or cannot be listed for runtime chat.</summary>
public sealed record TutorRuntimeReadinessReason(string Code, string Message);

/// <summary>The authoritative chat-readiness result for a visible tutor.</summary>
public sealed record TutorRuntimeReadinessResult(
    Guid TutorId,
    string TutorName,
    Guid? ActiveRevisionId,
    int? ActiveRevisionNumber,
    bool IsChatReady,
    IReadOnlyList<TutorRuntimeReadinessReason> Reasons);
