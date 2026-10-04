namespace Bitcraft.Domain.Tutoring;

/// <summary>
/// Generation-time evidence owned by one assistant message. Historical identifiers are values,
/// not relationships to live documents, chunks, tools or model catalog entries.
/// Retrieved context is not proof that the model cited or relied on that context.
/// </summary>
public sealed class TutorConversationMessageProvenance
{
    private TutorConversationMessageProvenance() { }
    private readonly List<TutorRetrievedKnowledgeEvidence> knowledgeEvidence = [];
    private readonly List<TutorToolExecutionEvidence> toolEvidence = [];

    public Guid TutorId { get; private set; }
    public Guid TutorRevisionId { get; private set; }
    public Guid TutorConfigurationSnapshotId { get; private set; }
    public Guid InteractionModelId { get; private set; }
    public string ProviderModelName { get; private set; } = "";
    public string ProviderType { get; private set; } = "";
    public DateTime CapturedUtc { get; private set; }
    public bool MemoryContextProvided { get; private set; }
    public int RetrievedMemoryCount { get; private set; }
    public IReadOnlyList<TutorRetrievedKnowledgeEvidence> KnowledgeEvidence => knowledgeEvidence.AsReadOnly();
    public IReadOnlyList<TutorToolExecutionEvidence> ToolEvidence => toolEvidence.AsReadOnly();

    public static TutorConversationMessageProvenance Capture(Guid tutorId, Guid revisionId, Guid snapshotId,
        Guid interactionModelId, string providerModelName, string providerType,
        IEnumerable<TutorRetrievedKnowledgeEvidence> knowledgeEvidence,
        IEnumerable<TutorToolExecutionEvidence> toolEvidence, int retrievedMemoryCount)
    {
        if (tutorId == Guid.Empty || revisionId == Guid.Empty || snapshotId == Guid.Empty || interactionModelId == Guid.Empty)
            throw new ArgumentException("Generation configuration identifiers are required.");
        if (string.IsNullOrWhiteSpace(providerModelName) || string.IsNullOrWhiteSpace(providerType))
            throw new ArgumentException("Safe model metadata is required.");
        if (retrievedMemoryCount < 0) throw new ArgumentOutOfRangeException(nameof(retrievedMemoryCount));
        // Use microsecond precision so generation-time and reloaded timestamps are identical
        // across stores, including PostgreSQL. Never rewrite a historical capture timestamp.
        var capturedUtc = DateTime.UtcNow;
        capturedUtc = capturedUtc.AddTicks(-(capturedUtc.Ticks % TimeSpan.TicksPerMicrosecond));
        var provenance = new TutorConversationMessageProvenance
        {
            TutorId = tutorId, TutorRevisionId = revisionId, TutorConfigurationSnapshotId = snapshotId,
            InteractionModelId = interactionModelId, ProviderModelName = providerModelName,
            ProviderType = providerType, CapturedUtc = capturedUtc,
            MemoryContextProvided = retrievedMemoryCount > 0, RetrievedMemoryCount = retrievedMemoryCount
        };
        provenance.knowledgeEvidence.AddRange(knowledgeEvidence);
        provenance.toolEvidence.AddRange(toolEvidence);
        return provenance;
    }
}

/// <summary>A bounded chunk excerpt retrieved and supplied as context, not an asserted model citation.</summary>
public sealed record TutorRetrievedKnowledgeEvidence
{
    public const int MaximumExcerptCharacters = 1_201;
    private TutorRetrievedKnowledgeEvidence() { }
    public Guid Id { get; private init; } = Guid.NewGuid();
    public Guid DocumentId { get; private init; }
    public string? DocumentName { get; private init; }
    public Guid DocumentChunkId { get; private init; }
    public int ChunkIndex { get; private init; }
    public string ContentExcerpt { get; private init; } = "";
    public int RetrievalRank { get; private init; }
    /// <summary>Cosine similarity; higher means closer. Null when the retriever has no score.</summary>
    public double? CosineSimilarity { get; private init; }
    public Guid? EmbeddingModelId { get; private init; }
    public string? EmbeddingProviderModelName { get; private init; }
    public int? EmbeddingDimensions { get; private init; }

    public static TutorRetrievedKnowledgeEvidence Capture(Guid documentId, string? documentName,
        Guid chunkId, int chunkIndex, string contentExcerpt, int retrievalRank, double? cosineSimilarity,
        Guid? embeddingModelId, string? embeddingProviderModelName, int? embeddingDimensions)
    {
        if (documentId == Guid.Empty || chunkId == Guid.Empty || chunkIndex < 0 || retrievalRank < 1)
            throw new ArgumentException("Document/chunk identifiers, index and retrieval rank are required.");
        if (cosineSimilarity.HasValue && !double.IsFinite(cosineSimilarity.Value))
            throw new ArgumentException("Retrieval similarity must be finite.");
        ArgumentNullException.ThrowIfNull(contentExcerpt);
        return new TutorRetrievedKnowledgeEvidence
        {
            DocumentId = documentId, DocumentName = documentName, DocumentChunkId = chunkId,
            ChunkIndex = chunkIndex, ContentExcerpt = contentExcerpt[..Math.Min(contentExcerpt.Length, MaximumExcerptCharacters)],
            RetrievalRank = retrievalRank, CosineSimilarity = cosineSimilarity,
            EmbeddingModelId = embeddingModelId, EmbeddingProviderModelName = embeddingProviderModelName,
            EmbeddingDimensions = embeddingDimensions
        };
    }
}

public enum TutorToolExecutionStatus { Succeeded, Failed, TimedOut }

/// <summary>Metadata for an actual invocation. Arguments, results and exception messages are excluded.</summary>
public sealed record TutorToolExecutionEvidence
{
    private TutorToolExecutionEvidence() { }
    public Guid Id { get; private init; } = Guid.NewGuid();
    public string ToolName { get; private init; } = "";
    public TutorToolExecutionStatus Status { get; private init; }
    public DateTime StartedUtc { get; private init; }
    public DateTime CompletedUtc { get; private init; }
    public double DurationMs { get; private init; }
    public string? ErrorCategory { get; private init; }

    public static TutorToolExecutionEvidence Capture(string toolName, TutorToolExecutionStatus status,
        DateTime startedUtc, DateTime completedUtc, double durationMs, string? errorCategory)
    {
        if (string.IsNullOrWhiteSpace(toolName) || !Enum.IsDefined(status) ||
            !double.IsFinite(durationMs) || durationMs < 0 || completedUtc < startedUtc)
            throw new ArgumentException("Valid tool execution metadata is required.");
        return new TutorToolExecutionEvidence
        {
            ToolName = toolName, Status = status, StartedUtc = startedUtc, CompletedUtc = completedUtc,
            DurationMs = durationMs,
            // Only the registry's fixed safe categories are allowed, never arbitrary exception text.
            ErrorCategory = errorCategory is "Timeout" or "ExecutionFailed" ? errorCategory : null
        };
    }
}
