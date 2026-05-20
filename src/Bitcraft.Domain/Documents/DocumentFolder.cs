using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Documents;

/// <summary>
/// Hierarchical folder used to organize per-user <see cref="Document"/> assets.
/// Folders form a tree rooted by entries with <see cref="ParentFolderId"/> = <c>null</c>.
/// Ownership is enforced at the Application layer via <see cref="BaseModel.CreatedById"/>.
/// Folders never reference Tutors; sharing happens at the Document level through
/// <c>KnowledgeSourceLink</c>.
/// </summary>
public class DocumentFolder : BaseModel
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Parent folder id; <c>null</c> for root folders.</summary>
    public Guid? ParentFolderId { get; set; }

    public DocumentFolder? ParentFolder { get; set; }
}
