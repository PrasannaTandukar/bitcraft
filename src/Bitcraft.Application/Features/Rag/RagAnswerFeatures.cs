using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Knowledge;
using MediatR;

namespace Bitcraft.Application.Features.Rag;

/// <summary>
/// Grounded answering use cases that compose document retrieval, embeddings,
/// and local LLM generation without duplicating those lower-level services.
/// </summary>
public static class RagAnswerFeatures
{
    private const int DefaultTopK = 5;
    private const int MaximumTopK = 10;
    private const string NoContextAnswer = "I could not find relevant information in the uploaded documents.";

    public sealed record AskRagQuestionCommand(
        string Question,
        string? Model,
        int? TopK) : IRequest<Result<RagAnswerResult>>;

    public sealed class AskRagQuestionHandler(
        IDocumentStore documentStore,
        IEmbeddingProvider embeddingProvider,
        IKnowledgeRetriever knowledgeRetriever,
        ILlmGenerationProvider generationProvider,
        ICurrentUserContext currentUser)
        : IRequestHandler<AskRagQuestionCommand, Result<RagAnswerResult>>
    {
        public async Task<Result<RagAnswerResult>> Handle(
            AskRagQuestionCommand request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return Result.Failure<RagAnswerResult>(new Error(
                    "Rag.QuestionRequired",
                    "Question is required."));
            }

            if (request.Model is not null && string.IsNullOrWhiteSpace(request.Model))
            {
                return Result.Failure<RagAnswerResult>(new Error(
                    "Ollama.ModelInvalid",
                    "Model cannot be empty when provided."));
            }

            var topK = request.TopK ?? DefaultTopK;
            if (topK is < 1 or > MaximumTopK)
            {
                return Result.Failure<RagAnswerResult>(new Error(
                    "Rag.TopKInvalid",
                    $"TopK must be between 1 and {MaximumTopK}."));
            }

            var question = request.Question.Trim();
            var model = request.Model?.Trim();
            var ownedDocuments = await documentStore.ListByCreatorAsync(currentUser.UserId, cancellationToken);
            var documentIds = ownedDocuments.Select(document => document.Id).ToList();

            float[] queryEmbedding;
            try
            {
                queryEmbedding = await embeddingProvider.GenerateEmbeddingAsync(
                    question,
                    embeddingModel: string.Empty,
                    cancellationToken);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                return Result.Failure<RagAnswerResult>(new Error(
                    "Ollama.Unavailable",
                    "Ollama embedding service is unavailable: " + ex.Message));
            }
            catch (TaskCanceledException ex)
            {
                return Result.Failure<RagAnswerResult>(new Error(
                    "Ollama.Unavailable",
                    "Ollama embedding request timed out: " + ex.Message));
            }
            catch (Exception ex)
            {
                return Result.Failure<RagAnswerResult>(new Error(
                    "Rag.EmbeddingFailed",
                    "Could not generate a question embedding: " + ex.Message));
            }

            List<KnowledgeChunk> chunks;
            try
            {
                chunks = documentIds.Count == 0
                    ? []
                    : await knowledgeRetriever.SearchNearestChunksAsync(
                        documentIds,
                        queryEmbedding,
                        topK,
                        cancellationToken);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Result.Failure<RagAnswerResult>(new Error(
                    "Rag.SearchFailed",
                    "Could not search uploaded document chunks: " + ex.Message));
            }

            if (chunks.Count == 0)
            {
                return Result.Success(new RagAnswerResult(
                    NoContextAnswer,
                    model ?? string.Empty,
                    []));
            }

            var prompt = BuildRagPrompt(question, chunks);
            var generation = await generationProvider.GenerateAsync(prompt, model, cancellationToken);
            if (generation.IsFailure)
            {
                return Result.Failure<RagAnswerResult>(generation.Error);
            }

            var documentNames = ownedDocuments.ToDictionary(document => document.Id, document => document.Name);
            var sources = chunks
                .Select(chunk => ToSourceResult(chunk, queryEmbedding, documentNames))
                .ToList();

            return Result.Success(new RagAnswerResult(
                generation.Value.Response,
                generation.Value.Model,
                sources));
        }

        internal static string BuildRagPrompt(string question, IReadOnlyList<KnowledgeChunk> chunks)
        {
            var context = chunks.Count == 0
                ? "No relevant uploaded document chunks were found."
                : string.Join("\n\n", chunks.Select((chunk, index) =>
                    $"[Source {index + 1} | Document {chunk.DocumentId} | Chunk {chunk.ChunkIndex}]\n{chunk.Content}"));

            return $$"""
            You are an AI tutor for students.
            Answer the user question using only the provided context from uploaded course documents.
            If the context does not contain enough information, say that the uploaded documents do not provide enough information.
            Do not invent facts.
            Explain clearly and simply.

            Context:
            {{context}}

            Question:
            {{question}}

            Answer:
            """;
        }

        private static RagSourceChunkResult ToSourceResult(
            KnowledgeChunk chunk,
            float[] queryEmbedding,
            IReadOnlyDictionary<Guid, string> documentNames)
        {
            var score = chunk.Embedding is null
                ? 0d
                : CalculateCosineSimilarity(queryEmbedding, chunk.Embedding);

            return new RagSourceChunkResult(
                chunk.DocumentId,
                documentNames.GetValueOrDefault(chunk.DocumentId),
                chunk.Id,
                chunk.ChunkIndex,
                chunk.Content,
                score);
        }

        private static double CalculateCosineSimilarity(float[] left, float[] right)
        {
            if (left.Length == 0 || left.Length != right.Length)
            {
                return 0d;
            }

            double dotProduct = 0;
            double leftMagnitude = 0;
            double rightMagnitude = 0;

            for (var index = 0; index < left.Length; index++)
            {
                dotProduct += left[index] * right[index];
                leftMagnitude += left[index] * left[index];
                rightMagnitude += right[index] * right[index];
            }

            if (leftMagnitude == 0 || rightMagnitude == 0)
            {
                return 0d;
            }

            return dotProduct / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
        }
    }
}
