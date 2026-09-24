using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Application.Features.Tutoring;

public sealed record TutorConversationMessageResponse(Guid Id, long Sequence, string Role, string Content,
    DateTime CreatedUtc, IReadOnlyList<Guid> AttachmentIds, string? ProviderModelName, string? ProviderType);
public sealed record TutorConversationResponse(Guid Id, Guid TutorId, Guid TutorRevisionId,
    Guid SnapshotId, string? Title, DateTime CreatedUtc, DateTime UpdatedUtc,
    IReadOnlyList<TutorConversationMessageResponse> Messages, int RevisionNumber, bool SupportsImageInput);

public sealed class TutorConversationService(ITutorRepository tutors, ITutorMembershipReader membership,
    ITutorConversationRepository conversations, ICurrentUserContext currentUser)
{
    private static readonly Error NotFound = new("TutorConversation.NotFound", "Conversation was not found.");
    private async Task<TutorRevision?> ResolveAsync(string slug, CancellationToken token)
    {
        var revision = await tutors.GetActiveRevisionBySlugAsync(slug.Trim(), token);
        if (revision is null || currentUser.UserId == Guid.Empty ||
            await membership.GetRoleAsync(revision.TutorId, currentUser.UserId, token) is null) return null;
        return revision;
    }

    public async Task<Result<TutorConversationResponse>> CreateAsync(string slug, CancellationToken token)
    {
        var revision = await ResolveAsync(slug, token);
        if (revision is null) return Result.Failure<TutorConversationResponse>(NotFound);
        var tutor = await tutors.GetByIdAsync(revision.TutorId, token);
        var snapshot = (await tutors.ListSnapshotsByTutorAsync(revision.TutorId, token))
            .Where(value => value.TutorRevisionId == revision.Id).OrderByDescending(value => value.CapturedUtc).FirstOrDefault();
        if (tutor?.Status != TutorStatus.Active || tutor.ActiveRevisionId != revision.Id || snapshot is null)
            return Result.Failure<TutorConversationResponse>(NotFound);
        var conversation = TutorConversation.Create(currentUser.UserId, revision.TutorId, revision.Id, snapshot.Id);
        await conversations.AddAsync(conversation, token);
        return Result.Success(await ToResponseAsync(conversation, token));
    }

    public async Task<Result<IReadOnlyList<TutorConversationResponse>>> ListAsync(string slug, CancellationToken token)
    {
        var revision = await ResolveAsync(slug, token);
        if (revision is null) return Result.Failure<IReadOnlyList<TutorConversationResponse>>(NotFound);
        var results = new List<TutorConversationResponse>();
        foreach (var conversation in await conversations.ListAsync(currentUser.UserId, revision.TutorId, token))
            results.Add(await ToResponseAsync(conversation, token));
        return Result.Success<IReadOnlyList<TutorConversationResponse>>(results);
    }

    public async Task<Result<TutorConversationResponse>> GetAsync(string slug, Guid id, CancellationToken token)
    {
        var revision = await ResolveAsync(slug, token);
        var conversation = revision is null ? null : await conversations.GetAsync(id, currentUser.UserId, revision.TutorId, token);
        return conversation is null ? Result.Failure<TutorConversationResponse>(NotFound) : Result.Success(await ToResponseAsync(conversation, token));
    }

    public async Task<Result> DeleteAsync(string slug, Guid id, CancellationToken token)
    {
        var revision = await ResolveAsync(slug, token);
        var conversation = revision is null ? null : await conversations.GetAsync(id, currentUser.UserId, revision.TutorId, token);
        if (conversation is null) return Result.Failure(NotFound);
        return await conversations.DeleteAsync(conversation, token) ? Result.Success() :
            Result.Failure(new Error("TutorConversation.Conflict", "This conversation changed. Reload it before deleting."));
    }

    private async Task<TutorConversationResponse> ToResponseAsync(TutorConversation value, CancellationToken token)
    {
        var snapshot = await tutors.GetSnapshotByIdAsync(value.TutorConfigurationSnapshotId, token);
        return new(value.Id, value.TutorId,
        value.TutorRevisionId, value.TutorConfigurationSnapshotId, value.Title, value.CreatedUtc, value.UpdatedUtc,
        value.Messages.OrderBy(message => message.Sequence).Select(message => new TutorConversationMessageResponse(
            message.Id, message.Sequence, message.Role, message.Content, message.CreatedUtc, message.AttachmentIds,
            message.ProviderModelName, message.ProviderType)).ToList(), snapshot?.VersionNumber ?? 0,
        snapshot?.ModelCapabilities.HasFlag(Bitcraft.Domain.Modeling.ModelCapabilities.ImageInput) == true);
    }
}
