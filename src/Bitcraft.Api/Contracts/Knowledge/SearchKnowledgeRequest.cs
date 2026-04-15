namespace Bitcraft.Api.Contracts.Knowledge;

public record SearchKnowledgeRequest(
    float[] QueryEmbedding,
    int TopK = 5
);
