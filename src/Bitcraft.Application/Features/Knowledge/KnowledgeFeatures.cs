using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Knowledge;

/// <summary>
/// RAG knowledge retrieval use cases. Operates on the already-embedded
/// document base via <see cref="IKnowledgeRetriever"/>. Document authoring
/// and (re-)embedding belongs in
/// <see cref="Documents.DocumentFeatures"/>.
/// </summary>
public static class KnowledgeFeatures
{
    public sealed record SearchKnowledgeQuery(Guid TutorId, float[] QueryEmbedding, int TopK = 5)
        : IRequest<Result<List<KnowledgeChunk>>>;

    public sealed class SearchKnowledgeHandler(
        IKnowledgeRetriever knowledgeRetriever,
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<SearchKnowledgeQuery, Result<List<KnowledgeChunk>>>
    {
        public async Task<Result<List<KnowledgeChunk>>> Handle(SearchKnowledgeQuery request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;

            if (request.QueryEmbedding is null || request.QueryEmbedding.Length == 0)
            {
                return Result.Failure<List<KnowledgeChunk>>(new Error("Knowledge.QueryEmbeddingRequired", "Query embedding is required."));
            }

            if (request.TopK <= 0 || request.TopK > 50)
            {
                return Result.Failure<List<KnowledgeChunk>>(new Error("Knowledge.TopKInvalid", "TopK must be between 1 and 50."));
            }

            var tutor = await tutorRepository.GetByIdWithDetailsAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<List<KnowledgeChunk>>(new Error("Tutor.NotFound", $"Tutor '{request.TutorId}' not found."));
            }

            // D2/D3: read-only retrieval → any membership role (Auditor, Editor, Owner).
            var role = await membership.GetRoleAsync(tutor.Id, userId, cancellationToken);
            if (role is null)
            {
                return Result.Failure<List<KnowledgeChunk>>(new Error("Tutor.Forbidden", "Tutor membership required."));
            }

            var documentIds = tutor.KnowledgeSources.Select(x => x.DocumentId).Distinct().ToList();
            if (documentIds.Count == 0)
            {
                return Result.Success(new List<KnowledgeChunk>());
            }

            var chunks = await knowledgeRetriever.SearchNearestChunksAsync(
                documentIds,
                request.QueryEmbedding,
                request.TopK,
                cancellationToken);
            return Result.Success(chunks);
        }
    }

    public sealed record VectorizeDocumentCommand(
        Guid DocumentId,
        string EmbeddingProvider,
        string EmbeddingModel,
        int MaxChunkLength = 1000,
        Guid? EmbeddingModelId = null) : IRequest<Result<VectorizeDocumentResult>>;

    public sealed record VectorizeDocumentResult(Guid DocumentId, int ChunkCount);

    public sealed class VectorizeDocumentHandler(
        IDocumentStore documentStore,
        IVectorStore vectorStore,
        IEmbeddingProvider embeddingProvider,
        IModelRepository modelRepository,
        ICurrentUserContext currentUser)
        : IRequestHandler<VectorizeDocumentCommand, Result<VectorizeDocumentResult>>
    {
        public async Task<Result<VectorizeDocumentResult>> Handle(VectorizeDocumentCommand request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;

            // D25 step 6: if an EmbeddingModelId is supplied, resolve the admin-managed model
            // and derive provider/model strings + expected dimensions from it. The legacy free-text
            // path is preserved for back-compat until callers migrate.
            string embeddingProviderInput = request.EmbeddingProvider;
            string embeddingModelInput = request.EmbeddingModel;
            Domain.Modeling.EmbeddingModel? embeddingModelEntity = null;
            if (request.EmbeddingModelId is { } modelId)
            {
                embeddingModelEntity = await modelRepository.GetEmbeddingModelByIdAsync(modelId, cancellationToken);
                if (embeddingModelEntity is null)
                {
                    return Result.Failure<VectorizeDocumentResult>(new Error("Knowledge.EmbeddingModelNotFound", $"Embedding model '{modelId}' not found."));
                }
                if (!embeddingModelEntity.IsEnabled)
                {
                    return Result.Failure<VectorizeDocumentResult>(new Error("Knowledge.EmbeddingModelDisabled", "Embedding model is disabled."));
                }
                if (embeddingModelEntity.EmbeddingDimensions <= 0)
                {
                    return Result.Failure<VectorizeDocumentResult>(new Error("Knowledge.EmbeddingDimensionsInvalid", "Embedding model has no declared dimensions."));
                }
                var engine = embeddingModelEntity.RuntimeEngine;
                if (engine is null)
                {
                    return Result.Failure<VectorizeDocumentResult>(new Error("Knowledge.RuntimeEngineMissing", "Embedding model is missing its runtime engine."));
                }
                if (!engine.IsEnabled)
                {
                    return Result.Failure<VectorizeDocumentResult>(new Error("Knowledge.RuntimeEngineDisabled", "Runtime engine is disabled."));
                }
                embeddingProviderInput = engine.ProviderType.ToString();
                embeddingModelInput = embeddingModelEntity.ProviderModelName;
            }

            if (string.IsNullOrWhiteSpace(embeddingProviderInput))
            {
                return Result.Failure<VectorizeDocumentResult>(new Error("Knowledge.EmbeddingProviderRequired", "Embedding provider is required."));
            }

            var embeddingProviderName = EmbeddingProviders.Normalize(embeddingProviderInput);
            if (embeddingProviderName is null)
            {
                return Result.Failure<VectorizeDocumentResult>(new Error("Knowledge.EmbeddingProviderUnsupported", "Only the Ollama embedding provider is implemented."));
            }

            if (string.IsNullOrWhiteSpace(embeddingModelInput))
            {
                return Result.Failure<VectorizeDocumentResult>(new Error("Knowledge.EmbeddingModelRequired", "Embedding model is required."));
            }

            if (request.MaxChunkLength <= 0)
            {
                return Result.Failure<VectorizeDocumentResult>(new Error("Knowledge.ChunkSizeInvalid", "Max chunk length must be positive."));
            }

            var document = await documentStore.GetByIdAsync(request.DocumentId, cancellationToken);
            if (document is null)
            {
                return Result.Failure<VectorizeDocumentResult>(new Error("Document.NotFound", $"Document '{request.DocumentId}' not found."));
            }

            if (document.CreatedById != userId)
            {
                return Result.Failure<VectorizeDocumentResult>(new Error("Document.Forbidden", "Requesting user does not own this document."));
            }

            if (string.IsNullOrWhiteSpace(document.RawText))
            {
                return Result.Failure<VectorizeDocumentResult>(new Error("Document.RawTextRequired", "Document raw text is required before vectorization."));
            }

            var chunks = SplitText(document.RawText, request.MaxChunkLength);
            var writes = new List<KnowledgeChunkWriteModel>(chunks.Count);

            for (var index = 0; index < chunks.Count; index++)
            {
                var content = chunks[index];
                var embedding = await embeddingProvider.GenerateEmbeddingAsync(content, embeddingModelInput, cancellationToken);
                if (embeddingModelEntity is not null && embedding.Length != embeddingModelEntity.EmbeddingDimensions)
                {
                    return Result.Failure<VectorizeDocumentResult>(new Error(
                        "Knowledge.EmbeddingDimensionsMismatch",
                        $"Embedding provider returned {embedding.Length} dimensions but model '{embeddingModelEntity.Name}' declares {embeddingModelEntity.EmbeddingDimensions}."));
                }
                writes.Add(new KnowledgeChunkWriteModel(
                    index,
                    content,
                    embedding,
                    embeddingModelInput.Trim(),
                    embeddingProviderName,
                    embeddingModelEntity?.Id,
                    embeddingModelEntity?.EmbeddingDimensions ?? embedding.Length));
            }

            await vectorStore.ReplaceDocumentChunksAsync(document.Id, writes, cancellationToken);

            return Result.Success(new VectorizeDocumentResult(document.Id, chunks.Count));
        }

        private static List<string> SplitText(string text, int maxChunkLength)
        {
            var normalized = text.Trim();
            var chunks = new List<string>();

            for (var start = 0; start < normalized.Length; start += maxChunkLength)
            {
                var length = Math.Min(maxChunkLength, normalized.Length - start);
                chunks.Add(normalized.Substring(start, length));
            }

            return chunks;
        }
    }
}
