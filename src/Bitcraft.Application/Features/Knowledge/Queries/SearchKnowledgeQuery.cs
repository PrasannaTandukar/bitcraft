namespace Bitcraft.Application.Features.Knowledge.Queries;

public record SearchKnowledgeQuery(float[] QueryEmbedding, int TopK = 5);
