using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Tutoring;

public class TutorModelConnection : BaseModel
{
    public Guid TutorId { get; set; }
    public Guid ModelCatalogEntryId { get; set; }
    public string? OverrideSettingsJson { get; set; }
    public Tutor? Tutor { get; set; }
    public ModelCatalogEntry? ModelCatalogEntry { get; set; }
}