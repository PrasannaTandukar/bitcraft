using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Documents;

public class Document : BaseModel
{
    public string Name { get; set; } = string.Empty;
    public string? SourcePath { get; set; }
    public string? MimeType { get; set; }
    public string? RawText { get; set; }
}
