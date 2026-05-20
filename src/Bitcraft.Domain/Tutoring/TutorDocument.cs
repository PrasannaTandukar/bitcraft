using Bitcraft.Domain.Common;
using Bitcraft.Domain.Documents;

namespace Bitcraft.Domain.Tutoring;

/// <summary>
/// M:N attachment of a <see cref="Document"/> to a <see cref="Tutor"/> (plan.erm.md §4.2).
/// Direct vs. folder attachment is recorded via <see cref="AttachedVia"/>; the materialized
/// effective document set is captured into the snapshot at activation time.
/// </summary>
public class TutorDocument : BaseModel
{
    public Guid TutorId { get; set; }
    public Tutor? Tutor { get; set; }

    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }

    public AttachmentSource AttachedVia { get; set; } = AttachmentSource.Direct;
}

/// <summary>Source of a <see cref="TutorDocument"/> link.</summary>
public enum AttachmentSource
{
    /// <summary>Document attached directly to the tutor.</summary>
    Direct = 0,

    /// <summary>Document attached transitively via a <see cref="DocumentFolder"/> linked to the tutor.</summary>
    Folder = 1,
}

/// <summary>
/// M:N attachment of an admin-side <see cref="DocumentFolder"/> to a <see cref="Tutor"/>
/// (plan.erm.md §4.2). Folder attachments expand at activation into per-document Snapshot rows.
/// </summary>
public class TutorDocumentFolder : BaseModel
{
    public Guid TutorId { get; set; }
    public Tutor? Tutor { get; set; }

    public Guid DocumentFolderId { get; set; }
    public DocumentFolder? DocumentFolder { get; set; }
}
