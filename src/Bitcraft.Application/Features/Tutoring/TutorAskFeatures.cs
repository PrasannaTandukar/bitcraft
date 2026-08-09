using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>
/// Compatibility adapter for the legacy /api/tutor/ask route. It deliberately contains no
/// retrieval, prompt, memory, or generation implementation; all execution uses ITutorRuntime.
/// </summary>
public static class TutorAskFeatures
{
    public sealed record AskTutorQuestionCommand(
        string Question,
        string? Model,
        bool UseMemory = false,
        bool SaveToMemory = false,
        int? TopK = null,
        Guid? TutorId = null,
        IReadOnlyList<TutorRuntimeConversationMessage>? History = null)
        : IRequest<Result<TutorAnswerResult>>;

    public sealed class AskTutorQuestionHandler(
        ITutorRuntime runtime,
        ITutorRepository tutors,
        ICurrentUserContext currentUser)
        : IRequestHandler<AskTutorQuestionCommand, Result<TutorAnswerResult>>
    {
        public async Task<Result<TutorAnswerResult>> Handle(
            AskTutorQuestionCommand request,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(request.Model))
            {
                return Result.Failure<TutorAnswerResult>(new Error(
                    "TutorRuntime.ModelOverrideNotAllowed",
                    "The model is selected by the tutor's active revision and cannot be overridden."));
            }

            var tutorId = request.TutorId;
            if (!tutorId.HasValue)
            {
                var visible = await tutors.ListByOwnerAsync(currentUser.UserId, cancellationToken);
                var active = visible.Where(tutor => tutor.ActiveRevisionId.HasValue).ToList();
                if (active.Count != 1)
                {
                    return Result.Failure<TutorAnswerResult>(new Error(
                        "TutorRuntime.TutorRequired",
                        "Select a configured tutor before asking a question."));
                }
                tutorId = active[0].Id;
            }

            var result = await runtime.ExecuteAsync(
                new TutorRuntimeRequest(
                    tutorId.Value,
                    null,
                    null,
                    request.Question,
                    currentUser.UserId,
                    true,
                    request.TopK ?? TutorRuntime.DefaultTopK,
                    TutorRuntimeExecutionMode.Interactive,
                    request.History),
                cancellationToken);
            if (result.IsFailure)
            {
                return Result.Failure<TutorAnswerResult>(result.Error);
            }

            var value = result.Value;
            return Result.Success(new TutorAnswerResult(
                value.Answer,
                value.Model.ProviderModelName,
                "configured-tutor",
                value.KnowledgeUsed,
                MemorySaved: false,
                value.Sources.Select(source => new TutorSourceResult(
                    "document",
                    source.ChunkId,
                    source.DocumentId,
                    source.DocumentName,
                    MemoryId: null,
                    source.ChunkId,
                    source.ChunkIndex,
                    source.Content,
                    source.Score)).ToList(),
                request.UseMemory || request.SaveToMemory
                    ? "Tutor-scoped long-term memory is not enabled; bounded request history was used instead."
                    : "Answer generated from the tutor's active revision."));
        }
    }
}
