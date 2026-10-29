using Bitcraft.Domain.Common;
using Bitcraft.Domain.Documents;

namespace Bitcraft.Domain.Tutoring;

public class KnowledgeSourceLink : BaseModel
{
    public Guid TutorId { get; set; }
    public Guid DocumentId { get; set; }
    public DateTime LinkedUtc { get; set; } = DateTime.UtcNow;
    public Tutor? Tutor { get; set; }
    public Document? Document { get; set; }
}
