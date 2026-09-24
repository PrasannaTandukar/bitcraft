using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Tutoring;

/// <summary>A durable conversation always executes the snapshot selected at creation.</summary>
public sealed class TutorConversation : BaseModel
{
    private TutorConversation() { }
    public Guid UserId { get; private set; }
    public Guid TutorId { get; private set; }
    public Guid TutorRevisionId { get; private set; }
    public Guid TutorConfigurationSnapshotId { get; private set; }
    public string? Title { get; private set; }
    public string? AgentSessionJson { get; private set; }
    public DateTime UpdatedUtc { get; private set; } = DateTime.UtcNow;
    public long Version { get; private set; }
    public List<TutorConversationMessage> Messages { get; private set; } = [];

    public static TutorConversation Create(Guid userId, Guid tutorId, Guid revisionId, Guid snapshotId)
    {
        var conversation = new TutorConversation { UserId = userId, TutorId = tutorId, TutorRevisionId = revisionId, TutorConfigurationSnapshotId = snapshotId };
        conversation.UpdateMetadata(userId);
        return conversation;
    }

    public void CompleteTurn(string sessionJson, string question, IReadOnlyList<Guid>? attachmentIds,
        string answer, string model, string provider)
    {
        AgentSessionJson = sessionJson;
        Title ??= string.IsNullOrWhiteSpace(question) ? "Image conversation" : question[..Math.Min(question.Length, 100)];
        var sequence = Messages.Count == 0 ? 0 : Messages.Max(message => message.Sequence);
        Messages.Add(TutorConversationMessage.Create(Id, ++sequence, "user", question, attachmentIds, null, null));
        Messages.Add(TutorConversationMessage.Create(Id, ++sequence, "assistant", answer, null, model, provider));
        foreach (var message in Messages.Where(message => message.CreatedById is null)) message.UpdateMetadata(UserId);
        Version++;
        UpdatedUtc = DateTime.UtcNow;
    }
}

public sealed class TutorConversationMessage : BaseModel
{
    private TutorConversationMessage() { }
    public Guid ConversationId { get; private set; }
    public long Sequence { get; private set; }
    public string Role { get; private set; } = "";
    public string Content { get; private set; } = "";
    public Guid[] AttachmentIds { get; private set; } = [];
    public string? ProviderModelName { get; private set; }
    public string? ProviderType { get; private set; }
    internal static TutorConversationMessage Create(Guid conversationId, long sequence, string role, string content,
        IReadOnlyList<Guid>? attachments, string? model, string? provider) => new()
    { ConversationId = conversationId, Sequence = sequence, Role = role, Content = content,
      AttachmentIds = attachments?.Distinct().ToArray() ?? [], ProviderModelName = model, ProviderType = provider };
}
