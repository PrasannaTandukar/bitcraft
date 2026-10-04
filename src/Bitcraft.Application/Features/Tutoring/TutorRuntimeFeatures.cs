using System.Runtime.CompilerServices;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

public static class TutorRuntimeFeatures
{
    public sealed record AskTutorRuntimeCommand(
        string ActivationSlug,
        string Question,
        int? TopK,
        IReadOnlyList<Guid>? AttachmentIds = null, Guid? ConversationId = null,
        bool UseMemory = false, bool SaveToMemory = false)
        : IRequest<Result<TutorRuntimeResult>>;

    public sealed class AskTutorRuntimeHandler(
        ITutorRepository tutors,
        ITutorRuntime runtime,
        ICurrentUserContext currentUser)
        : IRequestHandler<AskTutorRuntimeCommand, Result<TutorRuntimeResult>>
    {
        public async Task<Result<TutorRuntimeResult>> Handle(
            AskTutorRuntimeCommand request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ActivationSlug))
            {
                return Result.Failure<TutorRuntimeResult>(new Error(
                    "TutorRuntime.TutorNotFound",
                    "Tutor was not found."));
            }

            var revision = await tutors.GetActiveRevisionBySlugAsync(
                request.ActivationSlug.Trim(),
                cancellationToken);
            if (revision is null)
            {
                return Result.Failure<TutorRuntimeResult>(new Error(
                    "TutorRuntime.TutorNotFound",
                    "Tutor was not found."));
            }

            return await runtime.ExecuteAsync(
                new TutorRuntimeRequest(
                    revision.TutorId,
                    null,
                    null,
                    request.Question,
                    currentUser.UserId,
                    true,
                    request.TopK ?? TutorRuntime.DefaultTopK,
                    TutorRuntimeExecutionMode.Interactive,
                    AttachmentIds: request.AttachmentIds, ConversationId: request.ConversationId,
                    PersistConversation: true,
                    UseMemory: request.UseMemory, SaveToMemory: request.SaveToMemory),
                cancellationToken);
        }
    }

    public sealed record StreamTutorRuntimeCommand(
        string ActivationSlug,
        string Question,
        int? TopK,
        IReadOnlyList<Guid>? AttachmentIds = null,
        Guid? ConversationId = null,
        bool UseMemory = false,
        bool SaveToMemory = false) : IStreamRequest<TutorRuntimeStreamEvent>;

    public sealed class StreamTutorRuntimeHandler(
        ITutorRepository tutors,
        ITutorRuntime runtime,
        ICurrentUserContext currentUser)
        : IStreamRequestHandler<StreamTutorRuntimeCommand, TutorRuntimeStreamEvent>
    {
        public async IAsyncEnumerable<TutorRuntimeStreamEvent> Handle(StreamTutorRuntimeCommand request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ActivationSlug))
            {
                yield return StreamFailure("TutorRuntime.TutorNotFound", "Tutor was not found.");
                yield break;
            }
            var revision = await tutors.GetActiveRevisionBySlugAsync(request.ActivationSlug.Trim(), cancellationToken);
            if (revision is null)
            {
                yield return StreamFailure("TutorRuntime.TutorNotFound", "Tutor was not found.");
                yield break;
            }
            await foreach (var value in runtime.ExecuteStreamingAsync(new TutorRuntimeRequest(
                revision.TutorId, null, null, request.Question, currentUser.UserId, true,
                request.TopK ?? TutorRuntime.DefaultTopK, TutorRuntimeExecutionMode.Interactive,
                AttachmentIds: request.AttachmentIds, ConversationId: request.ConversationId,
                PersistConversation: true,
                UseMemory: request.UseMemory, SaveToMemory: request.SaveToMemory), cancellationToken))
                yield return value;
        }

        private static TutorRuntimeStreamEvent StreamFailure(string code, string description) =>
            new(1, TutorRuntimeStreamEventKind.Error, Error: new Error(code, description));
    }

    public sealed record ListRuntimeTutorsQuery : IRequest<IReadOnlyList<TutorRuntimeDescriptor>>;

    public sealed class ListRuntimeTutorsHandler(
        ITutorRepository tutors,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListRuntimeTutorsQuery, IReadOnlyList<TutorRuntimeDescriptor>>
    {
        public async Task<IReadOnlyList<TutorRuntimeDescriptor>> Handle(
            ListRuntimeTutorsQuery request,
            CancellationToken cancellationToken)
        {
            var visible = await tutors.ListByOwnerAsync(currentUser.UserId, cancellationToken);
            return visible
                .Select(tutor => TutorRuntimeReadinessEvaluator.Evaluate(tutor).Descriptor)
                .Where(value => value is not null)
                .Cast<TutorRuntimeDescriptor>()
                .OrderBy(value => value.TutorName)
                .ToList();
        }
    }

    public sealed record ListRuntimeTutorReadinessQuery : IRequest<IReadOnlyList<TutorRuntimeReadinessResult>>;

    public sealed class ListRuntimeTutorReadinessHandler(
        ITutorRepository tutors,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListRuntimeTutorReadinessQuery, IReadOnlyList<TutorRuntimeReadinessResult>>
    {
        public async Task<IReadOnlyList<TutorRuntimeReadinessResult>> Handle(
            ListRuntimeTutorReadinessQuery request,
            CancellationToken cancellationToken)
        {
            var visible = await tutors.ListByOwnerAsync(currentUser.UserId, cancellationToken);
            return visible
                .Select(tutor => TutorRuntimeReadinessEvaluator.Evaluate(tutor).Readiness)
                .OrderBy(value => value.TutorName)
                .ToList();
        }
    }
}
