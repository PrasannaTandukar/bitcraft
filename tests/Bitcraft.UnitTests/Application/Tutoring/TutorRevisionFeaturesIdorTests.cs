using Bitcraft.Application.Features.Tutoring;
using Bitcraft.Domain.Tutoring;
using Bitcraft.UnitTests.Application.Tutoring.Fakes;
using Xunit;

namespace Bitcraft.UnitTests.Application.Tutoring;

/// <summary>
/// Sec Ops finding M-1 — defense-in-depth for the <c>TutorRevision</c> feature handlers:
/// a request that targets <c>RevisionId</c> belonging to a different <c>TutorId</c> than the
/// one in the route MUST return an opaque <c>TutorRevision.NotFound</c>, never reveal the
/// real owner, and never reach the membership/permit branch.
/// </summary>
public class TutorRevisionFeaturesIdorTests
{
    private static TutorRevision NewPublishedRevisionForTutor(Guid tutorId, Guid actor)
    {
        var rev = new TutorRevision
        {
            TutorId = tutorId,
            VersionNumber = 1,
            Status = RevisionStatus.Draft,
            PromptVersionId = Guid.NewGuid(),
            InteractionModelId = Guid.NewGuid(),
        };
        rev.Publish(actor);
        return rev;
    }

    [Fact]
    public async Task GetRevisionByIdHandler_WhenRouteTutorIdDiffersFromRevisionTutorId_ReturnsNotFound()
    {
        var realTutorId = Guid.NewGuid();
        var attackerTutorId = Guid.NewGuid();
        var user = Guid.NewGuid();

        var repo = new FakeTutorRepository();
        var revision = NewPublishedRevisionForTutor(realTutorId, user);
        repo.Revisions.Add(revision);

        // Caller has Owner membership on the *attacker-supplied* tutor id — so if the handler
        // skipped the TutorId match check, membership alone would let them probe the revision.
        var membership = new FakeTutorMembershipReader(repo);
        membership.Grant(attackerTutorId, user, ResourceRole.Owner);

        var handler = new TutorRevisionFeatures.GetRevisionByIdHandler(
            repo, membership, new FakeCurrentUserContext(user));

        var result = await handler.Handle(
            new TutorRevisionFeatures.GetRevisionByIdQuery(attackerTutorId, revision.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TutorRevision.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task PublishRevisionHandler_WhenRouteTutorIdDiffersFromRevisionTutorId_ReturnsNotFound()
    {
        var realTutorId = Guid.NewGuid();
        var attackerTutorId = Guid.NewGuid();
        var user = Guid.NewGuid();

        var repo = new FakeTutorRepository();
        var revision = new TutorRevision
        {
            TutorId = realTutorId,
            VersionNumber = 1,
            Status = RevisionStatus.Draft,
            PromptVersionId = Guid.NewGuid(),
            InteractionModelId = Guid.NewGuid(),
        };
        repo.Revisions.Add(revision);

        var membership = new FakeTutorMembershipReader(repo);
        membership.Grant(attackerTutorId, user, ResourceRole.Owner);
        membership.Grant(realTutorId, user, ResourceRole.Owner);

        var handler = new TutorRevisionFeatures.PublishRevisionHandler(
            repo, membership, new FakeCurrentUserContext(user));

        var result = await handler.Handle(
            new TutorRevisionFeatures.PublishRevisionCommand(attackerTutorId, revision.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TutorRevision.NotFound", result.Error.Code);
        Assert.Equal(RevisionStatus.Draft, revision.Status);
    }
}
