using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Knowledge;

/// <summary>A user-owned, tutor-scoped semantic memory captured after a successful tutor answer.</summary>
public sealed class TutorMemory : BaseModel
{
    private TutorMemory() { }
    public Guid UserId { get; private set; }
    public Guid TutorId { get; private set; }
    public Guid? ConversationId { get; private set; }
    public Guid? TutorRevisionId { get; private set; }
    public Guid? TutorConfigurationSnapshotId { get; private set; }
    public string Question { get; private set; } = string.Empty;
    public string Answer { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public float[]? Embedding { get; private set; }
    public Guid? EmbeddingModelId { get; private set; }
    public int? EmbeddingDimensions { get; private set; }

    public static TutorMemory Create(Guid userId, Guid tutorId, Guid? conversationId,
        Guid tutorRevisionId, Guid tutorConfigurationSnapshotId, string question, string answer,
        string content, float[] embedding, Guid embeddingModelId, int embeddingDimensions)
    {
        if (userId == Guid.Empty || tutorId == Guid.Empty || tutorRevisionId == Guid.Empty ||
            tutorConfigurationSnapshotId == Guid.Empty || embeddingModelId == Guid.Empty)
            throw new ArgumentException("Memory provenance identifiers are required.");
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer) ||
            string.IsNullOrWhiteSpace(content) || embeddingDimensions <= 0 ||
            embedding.Length != embeddingDimensions || embedding.Any(value => !float.IsFinite(value)))
            throw new ArgumentException("Memory content and a valid embedding are required.");

        var memory = new TutorMemory
        {
            UserId = userId, TutorId = tutorId, ConversationId = conversationId,
            TutorRevisionId = tutorRevisionId, TutorConfigurationSnapshotId = tutorConfigurationSnapshotId,
            Question = question, Answer = answer, Content = content, Embedding = embedding.ToArray(),
            EmbeddingModelId = embeddingModelId, EmbeddingDimensions = embeddingDimensions
        };
        memory.UpdateMetadata(userId);
        return memory;
    }
}
