using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Domain.Tutoring;
using MediatR;
using Microsoft.Extensions.Logging;

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
        IRevisionEffectiveSetReader effectiveSetReader,
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

            var tutor = await tutorRepository.GetByIdAsync(request.TutorId, cancellationToken);
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

            var effective = await effectiveSetReader.GetEffectiveSetAsync(tutor.Id, cancellationToken);
            var documentIds = effective.DocumentIds;
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
        string? EmbeddingProvider = null,
        string? EmbeddingModel = null,
        int MaxChunkLength = 1000,
        Guid? EmbeddingModelId = null,
        int ChunkOverlap = 100) : IRequest<Result<VectorizeDocumentResult>>;

    public sealed record VectorizeDocumentResult(Guid DocumentId, int ChunkCount);

    public sealed class VectorizeDocumentHandler(
        IDocumentStore documentStore,
        IVectorStore vectorStore,
        IEmbeddingProvider embeddingProvider,
        IModelRepository modelRepository,
        ICurrentUserContext currentUser,
        DocumentIngestionLimits limits,
        ILogger<VectorizeDocumentHandler> logger)
        : IRequestHandler<VectorizeDocumentCommand, Result<VectorizeDocumentResult>>
    {
        public async Task<Result<VectorizeDocumentResult>> Handle(VectorizeDocumentCommand request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;

            if (request.MaxChunkLength is < 100 or > 8000)
            {
                return Result.Failure<VectorizeDocumentResult>(new Error(
                    "Knowledge.ChunkSizeInvalid",
                    "Maximum chunk length must be between 100 and 8000 characters."));
            }

            if (request.ChunkOverlap < 0 || request.ChunkOverlap >= request.MaxChunkLength)
            {
                return Result.Failure<VectorizeDocumentResult>(new Error(
                    "Knowledge.ChunkOverlapInvalid",
                    "Chunk overlap must be at least zero and less than the maximum chunk length."));
            }

            var document = await documentStore.GetByIdAsync(request.DocumentId, cancellationToken);
            if (document is null)
            {
                return Result.Failure<VectorizeDocumentResult>(new Error("Document.NotFound", "Document not found."));
            }

            if (document.CreatedById != userId)
            {
                return Result.Failure<VectorizeDocumentResult>(new Error("Document.Forbidden", "Requesting user does not own this document."));
            }

            if (string.IsNullOrWhiteSpace(document.RawText))
            {
                return Result.Failure<VectorizeDocumentResult>(new Error("Document.RawTextRequired", "Document raw text is required before vectorization."));
            }

            // D25 step 6: if an EmbeddingModelId is supplied, resolve the admin-managed model
            // and derive provider/model strings + expected dimensions from it. The legacy free-text
            // path is preserved for back-compat until callers migrate.
            string? embeddingProviderInput = request.EmbeddingProvider;
            string? embeddingModelInput = request.EmbeddingModel;
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
                return Result.Failure<VectorizeDocumentResult>(new Error(
                    "Knowledge.EmbeddingProviderUnsupported",
                    $"The embedding provider '{embeddingProviderInput}' is not supported. Supported providers are: {string.Join(", ", EmbeddingProviders.All)}."));
            }

            if (string.IsNullOrWhiteSpace(embeddingModelInput))
            {
                return Result.Failure<VectorizeDocumentResult>(new Error("Knowledge.EmbeddingModelRequired", "Embedding model is required."));
            }

            var split = DocumentTextChunker.Split(
                document.RawText,
                request.MaxChunkLength,
                request.ChunkOverlap,
                limits.MaxVectorizationChunks);
            if (split.IsFailure)
            {
                return Result.Failure<VectorizeDocumentResult>(split.Error);
            }

            var chunks = split.Value;
            var writes = new List<KnowledgeChunkWriteModel>(chunks.Count);

            for (var index = 0; index < chunks.Count; index++)
            {
                var content = chunks[index];
                float[] embedding;
                try
                {
                    embedding = await embeddingProvider.GenerateEmbeddingAsync(
                        new EmbeddingGenerationRequest(
                            content,
                            embeddingModelInput,
                            embeddingModelEntity?.RuntimeEngine?.Endpoint,
                            embeddingModelEntity?.RuntimeEngine?.ApiKeySecretRef,
                            embeddingProviderName),
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (EmbeddingProviderException ex)
                {
                    logger.LogWarning(
                        ex,
                        "Embedding generation failed for document {DocumentId}, chunk {ChunkIndex}, model {EmbeddingModel}. FailureKind={FailureKind}",
                        document.Id,
                        index,
                        embeddingModelInput,
                        ex.Kind);
                    return Result.Failure<VectorizeDocumentResult>(ToEmbeddingError(ex.Kind));
                }
                catch (HttpRequestException ex)
                {
                    logger.LogWarning(
                        ex,
                        "Embedding service unavailable for document {DocumentId}, chunk {ChunkIndex}, model {EmbeddingModel}.",
                        document.Id,
                        index,
                        embeddingModelInput);
                    return Result.Failure<VectorizeDocumentResult>(ToEmbeddingError(EmbeddingProviderFailureKind.ServiceUnavailable));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Embedding request failed for document {DocumentId}, chunk {ChunkIndex}, model {EmbeddingModel}.",
                        document.Id,
                        index,
                        embeddingModelInput);
                    return Result.Failure<VectorizeDocumentResult>(ToEmbeddingError(EmbeddingProviderFailureKind.RequestFailed));
                }

                if (embedding.Length == 0)
                {
                    logger.LogWarning(
                        "Embedding response was empty for document {DocumentId}, chunk {ChunkIndex}, model {EmbeddingModel}.",
                        document.Id,
                        index,
                        embeddingModelInput);
                    return Result.Failure<VectorizeDocumentResult>(ToEmbeddingError(EmbeddingProviderFailureKind.ResponseInvalid));
                }

                if (embeddingModelEntity is not null && embedding.Length != embeddingModelEntity.EmbeddingDimensions)
                {
                    logger.LogWarning(
                        "Embedding dimensions mismatch for document {DocumentId}, chunk {ChunkIndex}, model {EmbeddingModel}. Expected={ExpectedDimensions}, Actual={ActualDimensions}",
                        document.Id,
                        index,
                        embeddingModelInput,
                        embeddingModelEntity.EmbeddingDimensions,
                        embedding.Length);
                    return Result.Failure<VectorizeDocumentResult>(new Error(
                        "Knowledge.EmbeddingDimensionsMismatch",
                        "The embedding response dimensions do not match the selected model configuration."));
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

        private static Error ToEmbeddingError(EmbeddingProviderFailureKind kind)
        {
            return kind switch
            {
                EmbeddingProviderFailureKind.ServiceUnavailable => new Error(
                    "Knowledge.EmbeddingServiceUnavailable",
                    "The embedding service is unavailable. Confirm that the runtime is running and try again."),
                EmbeddingProviderFailureKind.ResponseInvalid => new Error(
                    "Knowledge.EmbeddingResponseInvalid",
                    "The embedding service returned an invalid response for the selected model."),
                _ => new Error(
                    "Knowledge.EmbeddingRequestFailed",
                    "The embedding service could not process this document. Confirm that the selected model is installed and the runtime is available.")
            };
        }
    }
}
