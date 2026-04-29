namespace Bitcraft.Domain.Tutoring;

/// <summary>Lifecycle state of a tutor.</summary>
public enum TutorStatus
{
    /// <summary>Editable working copy. Not exposed to end-users.</summary>
    Draft = 0,
    /// <summary>Promoted and serving end-users.</summary>
    Active = 1,
    /// <summary>Inactive and not serving end-users.</summary>
    Inactive = 2,
    /// <summary>Disabled by an administrator.</summary>
    Disabled = 3
}
