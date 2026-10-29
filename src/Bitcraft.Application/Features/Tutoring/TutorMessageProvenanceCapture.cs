using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>Maps execution evidence only. Instructions, session state, tool payloads and raw memory never enter provenance.</summary>
internal static class TutorMessageProvenanceCapture
{
    public static TutorConversationMessageProvenance Capture(TutorAgentRequest request, TutorAgentResponse response)
    {
        var retrieval = response.Knowledge ?? TutorKnowledgeResult.Empty;
        var embedding = retrieval.EmbeddingModel;
        var knowledge = retrieval.ContextSources.Select(source => TutorRetrievedKnowledgeEvidence.Capture(
            source.DocumentId, source.DocumentName, source.ChunkId, source.ChunkIndex, source.Content,
            retrieval.Sources.TakeWhile(candidate => candidate != source).Count() + 1,
            source.Score, embedding?.Id, embedding?.ProviderModelName, embedding?.Dimensions));
        var tools = (response.ToolActivities ?? []).Where(activity => activity.Status != TutorToolCallStatus.Running)
            .Select(activity => TutorToolExecutionEvidence.Capture(activity.ToolName, activity.Status switch
            {
                TutorToolCallStatus.Succeeded => TutorToolExecutionStatus.Succeeded,
                TutorToolCallStatus.Failed => TutorToolExecutionStatus.Failed,
                TutorToolCallStatus.TimedOut => TutorToolExecutionStatus.TimedOut,
                _ => throw new ArgumentOutOfRangeException(nameof(activity.Status))
            }, activity.StartedUtc, activity.CompletedUtc, activity.DurationMs, activity.ErrorCategory));
        return TutorConversationMessageProvenance.Capture(request.TutorId, request.TutorRevisionId,
            request.TutorConfigurationSnapshotId, request.Model.ModelId, response.ProviderModelName,
            response.ProviderType.ToString(), knowledge, tools, response.Memory?.ContextMemoryCount ?? 0);
    }
}
