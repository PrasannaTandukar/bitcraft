namespace Bitcraft.Api.Contracts.Tutoring;

public sealed record TutorConversationResponse(Guid Id, Guid TutorId, Guid TutorRevisionId, Guid SnapshotId,
    string? Title, DateTime CreatedUtc, DateTime UpdatedUtc, IReadOnlyList<TutorConversationStoredMessageResponse> Messages, int RevisionNumber, bool SupportsImageInput);
public sealed record TutorConversationStoredMessageResponse(Guid Id, long Sequence, string Role, string Content,
    DateTime CreatedUtc, IReadOnlyList<Guid> AttachmentIds, string? ProviderModelName, string? ProviderType)
{
    /// <summary>Null when generation-time evidence was not captured for this message.</summary>
    public TutorMessageProvenanceResponse? Provenance { get; init; }
}
