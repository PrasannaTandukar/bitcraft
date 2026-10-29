using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Documents;

public class Document : BaseModel
{
    public string Name { get; set; } = string.Empty;
    public string? SourcePath { get; set; }
    public string? MimeType { get; set; }
    public string? RawText { get; set; }

    /// <summary>Optional parent folder for organising the owner's document tree. <c>null</c> places the document at the user's root.</summary>
    public Guid? FolderId { get; set; }

    public DocumentFolder? Folder { get; set; }
}
