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
        int MaxChunkLength = 1000) : IRequest<Result<VectorizeDocumentResult>>;

    public sealed record VectorizeDocumentResult(Guid DocumentId, int ChunkCount);

    public sealed class VectorizeDocumentHandler(
        IDocumentStore documentStore,
        IVectorStore vectorStore,
        IEmbeddingProvider embeddingProvider,
        ICurrentUserContext currentUser)
        : IRequestHandler<VectorizeDocumentCommand, Result<VectorizeDocumentResult>>
    {
        public async Task<Result<VectorizeDocumentResult>> Handle(VectorizeDocumentCommand request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;

            if (string.IsNullOrWhiteSpace(request.EmbeddingProvider))
            {
                return Result.Failure<VectorizeDocumentResult>(new Error("Knowledge.EmbeddingProviderRequired", "Embedding provider is required."));
            }

            var embeddingProviderName = EmbeddingProviders.Normalize(request.EmbeddingProvider);
            if (embeddingProviderName is null)
            {
                return Result.Failure<VectorizeDocumentResult>(new Error("Knowledge.EmbeddingProviderUnsupported", "Only the Ollama embedding provider is implemented."));
            }

            if (string.IsNullOrWhiteSpace(request.EmbeddingModel))
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
                var embedding = await embeddingProvider.GenerateEmbeddingAsync(content, request.EmbeddingModel, cancellationToken);
                writes.Add(new KnowledgeChunkWriteModel(
                    index,
                    content,
                    embedding,
                    request.EmbeddingModel.Trim(),
                    embeddingProviderName));
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
