using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;

namespace Bitcraft.Infrastructure.ExternalServices.Agents;

/// <summary>Scoped dependencies only; mutable retrieval metadata belongs to each newly created provider.</summary>
public sealed class TutorKnowledgeContextProviderFactory(
    IModelRepository models,
    IRuntimeEngineRepository runtimeEngines,
    IDocumentStore documents,
    IEmbeddingProvider embeddings,
    IKnowledgeRetriever knowledge)
{
    public TutorKnowledgeContextProvider Create(TutorKnowledgeConfiguration? configuration) =>
        new(configuration, models, runtimeEngines, documents, embeddings, knowledge);
}
