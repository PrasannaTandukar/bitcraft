namespace Bitcraft.Domain.Tutoring;

/// <summary>
/// Resource-scoped role assigned to a user on a specific resource (e.g. a tutor) via a membership row.
/// Distinct from the global identity roles in <c>AppRoles</c>; resource roles control authorization
/// within a single resource boundary rather than across the application.
/// </summary>
/// <remarks>
/// Ordering: <see cref="Owner"/> &gt; <see cref="Editor"/> &gt; <see cref="Auditor"/>.
/// Persisted as <see cref="int"/> in EF; do not reorder existing members.
/// </remarks>
public enum ResourceRole
{
    /// <summary>Read-only access. Cannot modify state or membership.</summary>
    Auditor = 0,

    /// <summary>Read/write access to resource content but cannot manage membership or delete the resource.</summary>
    Editor = 1,

    /// <summary>Full control including membership management and deletion. At least one Owner must exist at all times.</summary>
    Owner = 2,
}
