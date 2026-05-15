using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Knowledge;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>
/// Unified tutor answering flow. Tutor memory can improve an answer, but local
/// generation remains responsible for answering even when no useful context exists.
/// </summary>
public static class TutorAskFeatures
{
    private const int DefaultTopK = 5;
    private const int MaximumTopK = 10;
    private const int MaximumSourceCharacters = 1200;
    private const double MinimumUsefulSimilarityScore = 0.55d;

    public sealed record AskTutorQuestionCommand(
        string Question,
        string? Model,
        bool UseMemory = false,
        bool SaveToMemory = false,
        int? TopK = null) : IRequest<Result<TutorAnswerResult>>;

    public sealed class AskTutorQuestionHandler(
        IDocumentStore documentStore,
        IEmbeddingProvider embeddingProvider,
        IKnowledgeRetriever knowledgeRetriever,
        ITutorMemoryStore tutorMemoryStore,
        ILlmGenerationProvider generationProvider,
        ICurrentUserContext currentUser,
        ILogger<AskTutorQuestionHandler> logger)
        : IRequestHandler<AskTutorQuestionCommand, Result<TutorAnswerResult>>
    {
        public async Task<Result<TutorAnswerResult>> Handle(
            AskTutorQuestionCommand request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return Result.Failure<TutorAnswerResult>(new Error(
                    "Tutor.QuestionRequired",
                    "Question is required."));
            }

            if (request.Model is not null && string.IsNullOrWhiteSpace(request.Model))
            {
                return Result.Failure<TutorAnswerResult>(new Error(
                    "Tutor.ModelInvalid",
                    "Model cannot be empty when provided."));
            }

            var topK = request.TopK ?? DefaultTopK;
            if (topK is < 1 or > MaximumTopK)
            {
                return Result.Failure<TutorAnswerResult>(new Error(
                    "Tutor.TopKInvalid",
                    $"TopK must be between 1 and {MaximumTopK}."));
            }

            var question = request.Question.Trim();
            var model = request.Model?.Trim();
            logger.LogInformation(
                "Tutor ask received for user {UserId}. SaveToMemory={SaveToMemory}, UseMemory={UseMemory}.",
                currentUser.UserId,
                request.SaveToMemory,
                request.UseMemory);
            var memorySearchFailed = false;
            var sources = new List<TutorSourceResult>();

            if (request.UseMemory)
            {
                var retrieval = await TryRetrieveTutorMemoryAsync(question, topK, cancellationToken);
                memorySearchFailed = retrieval.SearchFailed;
                sources = retrieval.Sources;
            }

            var contextUsed = sources.Count > 0;
            var prompt = contextUsed
                ? BuildMemoryPrompt(question, sources)
                : BuildGeneralPrompt(question);

            var generation = await generationProvider.GenerateAsync(prompt, model, cancellationToken);
            if (generation.IsFailure)
            {
                return Result.Failure<TutorAnswerResult>(generation.Error);
            }

            var memorySaved = false;
            var memorySaveFailed = false;
            var memorySaveSkippedForPrivacy = false;
            if (request.SaveToMemory)
            {
                if (CanSaveToMemory(question, generation.Value.Response))
                {
                    memorySaved = await TrySaveConversationAsync(question, generation.Value.Response, cancellationToken);
                    memorySaveFailed = !memorySaved;
                }
                else
                {
                    memorySaveSkippedForPrivacy = true;
                    logger.LogInformation("Tutor memory save skipped because the conversation may contain sensitive text.");
                }
            }

            var mode = request.UseMemory ? "memory" : "general";
            var message = BuildMessage(
                request.UseMemory,
                contextUsed,
                memorySearchFailed,
                request.SaveToMemory,
                memorySaved,
                memorySaveFailed,
                memorySaveSkippedForPrivacy);

            return Result.Success(new TutorAnswerResult(
                generation.Value.Response,
                generation.Value.Model,
                mode,
                contextUsed,
                memorySaved,
                sources,
                message));
        }

        internal static string BuildGeneralPrompt(string question) => $$"""
            You are an AI tutor for this COMP6015 project.
            In this project, RAG means Retrieval-Augmented Generation.
            RAG is a method where relevant context is retrieved from documents or memory and then provided to the language model before generating an answer.
            Answer clearly and simply.

            Question:
            {{question}}

            Answer:
            """;

        internal static string BuildMemoryPrompt(string question, IReadOnlyList<TutorSourceResult> sources)
        {
            var memory = string.Join("\n\n", sources.Select((source, index) =>
                $"[Source {index + 1}: {source.SourceType}]\n{TrimSourceContent(source.Content)}"));

            return $$"""
            You are an AI tutor for this COMP6015 project.
            In this project, RAG means Retrieval-Augmented Generation.
            Use the provided tutor memory if it is relevant.
            Tutor memory may include uploaded document chunks and saved previous conversations.
            If memory is not relevant, still answer using your general knowledge.
            Be clear and do not invent fake sources.

            Tutor memory:
            {{memory}}

            Question:
            {{question}}

            Answer:
            """;
        }

        internal static bool ContainsObviousSecret(string text) =>
            text.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("api key", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("secret", StringComparison.OrdinalIgnoreCase);

        private async Task<(List<TutorSourceResult> Sources, bool SearchFailed)> TryRetrieveTutorMemoryAsync(
            string question,
            int topK,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation("Tutor memory search started for user {UserId} with TopK {TopK}.", currentUser.UserId, topK);
                var queryEmbedding = await embeddingProvider.GenerateEmbeddingAsync(
                    question,
                    embeddingModel: string.Empty,
                    cancellationToken);
                logger.LogInformation("Tutor memory query embedding generated with {Dimensions} dimensions.", queryEmbedding.Length);
                var ownedDocuments = await documentStore.ListByCreatorAsync(currentUser.UserId, cancellationToken);
                var documentIds = ownedDocuments.Select(document => document.Id).ToList();
                var documentChunks = documentIds.Count == 0
                    ? []
                    : await knowledgeRetriever.SearchNearestChunksAsync(documentIds, queryEmbedding, topK, cancellationToken);
                var conversationMemories = await tutorMemoryStore.SearchNearestMemoriesAsync(
                    currentUser.UserId,
                    queryEmbedding,
                    topK,
                    cancellationToken);

                logger.LogInformation(
                    "Tutor memory search returned {DocumentChunkCount} document chunks and {ConversationMemoryCount} saved conversations before filtering.",
                    documentChunks.Count,
                    conversationMemories.Count);

                var documentNames = ownedDocuments.ToDictionary(document => document.Id, document => document.Name);
                var documentSources = documentChunks.Select(chunk => ToDocumentSource(chunk, queryEmbedding, documentNames));
                var conversationSources = conversationMemories.Select(memory => ToConversationSource(memory, queryEmbedding));
                var sources = documentSources
                    .Concat(conversationSources)
                    .Where(source => source.Score >= MinimumUsefulSimilarityScore)
                    .OrderByDescending(source => source.Score)
                    .Take(topK)
                    .ToList();

                logger.LogInformation("Tutor memory search kept {SourceCount} relevant sources after similarity filtering.", sources.Count);
                return (sources, SearchFailed: false);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Tutor memory search failed for user {UserId}.", currentUser.UserId);
                return ([], SearchFailed: true);
            }
        }

        private async Task<bool> TrySaveConversationAsync(
            string question,
            string answer,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Tutor memory save started for user {UserId}.", currentUser.UserId);
            var content = BuildConversationMemoryContent(question, answer);
            logger.LogInformation("Tutor memory content prepared for user {UserId}: {MemoryContent}", currentUser.UserId, content);

            float[] embedding;
            try
            {
                logger.LogInformation("Tutor memory embedding generation started for user {UserId}.", currentUser.UserId);
                embedding = await embeddingProvider.GenerateEmbeddingAsync(
                    content,
                    embeddingModel: string.Empty,
                    cancellationToken);
                logger.LogInformation("Tutor memory embedding generation succeeded with {Dimensions} dimensions.", embedding.Length);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Tutor memory embedding generation failed for user {UserId}.", currentUser.UserId);
                return false;
            }

            try
            {
                logger.LogInformation("Tutor memory database save started for user {UserId}.", currentUser.UserId);
                var memory = await tutorMemoryStore.AddAsync(new TutorMemory
                {
                    Question = question,
                    Answer = answer.Trim(),
                    Content = content,
                    Embedding = embedding
                }, cancellationToken);
                logger.LogInformation("Tutor memory save succeeded for user {UserId} with memory {MemoryId}.", currentUser.UserId, memory.Id);
                return true;
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Tutor memory database save failed for user {UserId}.", currentUser.UserId);
                return false;
            }
        }

        private static TutorSourceResult ToDocumentSource(
            KnowledgeChunk chunk,
            float[] queryEmbedding,
            IReadOnlyDictionary<Guid, string> documentNames) =>
            new(
                "document",
                chunk.Id,
                chunk.DocumentId,
                documentNames.GetValueOrDefault(chunk.DocumentId),
                null,
                chunk.Id,
                chunk.ChunkIndex,
                chunk.Content,
                CalculateCosineSimilarity(queryEmbedding, chunk.Embedding));

        private static TutorSourceResult ToConversationSource(TutorMemory memory, float[] queryEmbedding) =>
            new(
                "conversation",
                memory.Id,
                null,
                null,
                memory.Id,
                null,
                null,
                memory.Content,
                CalculateCosineSimilarity(queryEmbedding, memory.Embedding));

        private static string BuildConversationMemoryContent(string question, string answer) => $$"""
            User asked:
            {{question}}

            Tutor answered:
            {{answer.Trim()}}
            """;

        private static string TrimSourceContent(string content) => content.Length <= MaximumSourceCharacters
            ? content
            : content[..MaximumSourceCharacters] + "…";

        private static bool CanSaveToMemory(string question, string answer) =>
            !string.IsNullOrWhiteSpace(question) &&
            !string.IsNullOrWhiteSpace(answer) &&
            !ContainsObviousSecret(question) &&
            !ContainsObviousSecret(answer);

        private static string BuildMessage(
            bool useMemory,
            bool contextUsed,
            bool memorySearchFailed,
            bool saveRequested,
            bool memorySaved,
            bool memorySaveFailed,
            bool memorySaveSkippedForPrivacy)
        {
            if (memorySearchFailed && memorySaveFailed)
            {
                return "Answered using the local model only. Tutor memory search and save are currently unavailable.";
            }

            string answerMessage;
            if (!useMemory)
            {
                answerMessage = "General Ollama answer generated without tutor memory.";
            }
            else if (contextUsed)
            {
                answerMessage = "Answer generated using retrieved tutor memory.";
            }
            else if (memorySearchFailed)
            {
                answerMessage = "Tutor memory could not be searched, so the tutor answered using the local model only.";
            }
            else
            {
                answerMessage = "No relevant tutor memory was found, so the tutor answered using the local model only.";
            }

            if (memorySaved)
            {
                return answerMessage + " Saved to tutor memory.";
            }

            if (memorySaveFailed)
            {
                return answerMessage + " Answer generated, but it could not be saved to tutor memory.";
            }

            if (saveRequested && memorySaveSkippedForPrivacy)
            {
                return answerMessage + " The conversation was not saved because it may contain sensitive information.";
            }

            return answerMessage;
        }

        private static double CalculateCosineSimilarity(float[] left, float[]? right)
        {
            if (right is null || left.Length == 0 || left.Length != right.Length)
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
