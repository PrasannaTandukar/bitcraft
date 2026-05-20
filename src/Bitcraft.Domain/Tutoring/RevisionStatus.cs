namespace Bitcraft.Domain.Tutoring;

/// <summary>
/// Lifecycle state of a <see cref="TutorRevision"/> per plan.erm.md §7.2.
/// Transitions: Draft → Published → Active; Published/Active → Archived; Draft → (hard delete).
/// </summary>
public enum RevisionStatus
{
    /// <summary>Editable working copy. Mutable. Allowed to be hard-deleted.</summary>
    Draft = 0,

    /// <summary>Immutable, promoted but not yet serving runtime.</summary>
    Published = 1,

    /// <summary>Serving runtime. At most one Active revision per tutor.</summary>
    Active = 2,

    /// <summary>Historical, no longer serving runtime.</summary>
    Archived = 3,
}
