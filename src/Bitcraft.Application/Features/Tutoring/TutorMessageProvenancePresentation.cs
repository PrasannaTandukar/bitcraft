using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>Allowlisted message evidence. Never resolves current documents, model catalog labels or configuration secrets.</summary>
internal static class TutorMessageProvenancePresentation
{
    public static TutorMessageProvenanceResponse? Map(TutorConversationMessageProvenance? value, int? revisionNumber)
    {
        if (value is null) return null;
        // Derive stable local ordinals from stored identities; never disclose live document IDs.
        var documentGroups = value.KnowledgeEvidence.OrderBy(evidence => evidence.RetrievalRank)
            .Select(evidence => evidence.DocumentId).Distinct()
            .Select((id, index) => (id, key: $"document-{index + 1}"))
            .ToDictionary(group => group.id, group => group.key);
        var chunks = value.KnowledgeEvidence.OrderBy(evidence => evidence.RetrievalRank)
            .Select(evidence => new TutorRetrievedChunkEvidenceResponse(evidence.DocumentName,
                evidence.ChunkIndex, evidence.RetrievalRank, evidence.ContentExcerpt, evidence.CosineSimilarity)
                { DocumentGroupKey = documentGroups[evidence.DocumentId] }).ToList();
        return new(new(chunks.Count, chunks, "cosineSimilarity"),
            new(value.MemoryContextProvided, value.RetrievedMemoryCount),
            value.ToolEvidence.OrderBy(evidence => evidence.StartedUtc).Select(evidence => new TutorToolProvenanceResponse(
                evidence.ToolName, evidence.Status.ToString(), evidence.Status == TutorToolExecutionStatus.Succeeded,
                evidence.DurationMs, evidence.ErrorCategory is "Timeout" or "ExecutionFailed" ? evidence.ErrorCategory : null)).ToList(),
            new(value.TutorId, value.TutorRevisionId, revisionNumber, value.TutorConfigurationSnapshotId,
                value.InteractionModelId, value.ProviderModelName, value.ProviderType), value.CapturedUtc);
    }
}
