using Bitcraft.Domain.Tutoring;
using Xunit;

namespace Bitcraft.UnitTests.Domain.Tutoring;

/// <summary>D25 step 7 — Publish/Activate/Archive state-machine invariants on <see cref="TutorRevision"/>.</summary>
public class TutorRevisionDomainTests
{
    private static TutorRevision NewDraft() => new()
    {
        TutorId = Guid.NewGuid(),
        VersionNumber = 1,
        Status = RevisionStatus.Draft,
        PromptVersionId = Guid.NewGuid(),
        InteractionModelId = Guid.NewGuid(),
    };

    [Fact]
    public void Publish_FromDraft_TransitionsToPublishedAndStamps()
    {
        var rev = NewDraft();
        var actor = Guid.NewGuid();

        rev.Publish(actor);

        Assert.Equal(RevisionStatus.Published, rev.Status);
        Assert.NotNull(rev.PublishedUtc);
        Assert.Equal(actor, rev.PublishedById);
    }

    [Fact]
    public void Publish_WithoutPromptOrModel_Throws()
    {
        var rev = new TutorRevision { Status = RevisionStatus.Draft };
        Assert.Throws<InvalidOperationException>(() => rev.Publish(Guid.NewGuid()));
    }

    [Fact]
    public void Publish_NonDraft_Throws()
    {
        var rev = NewDraft();
        rev.Publish(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => rev.Publish(Guid.NewGuid()));
    }

    [Fact]
    public void Publish_EmptyUserId_Throws()
    {
        var rev = NewDraft();
        Assert.Throws<ArgumentException>(() => rev.Publish(Guid.Empty));
    }

    [Fact]
    public void Activate_FromPublished_TransitionsAndAssignsSlug()
    {
        var rev = NewDraft();
        rev.Publish(Guid.NewGuid());

        rev.Activate(Guid.NewGuid(), "my-tutor", previousActivationSlug: "old-slug");

        Assert.Equal(RevisionStatus.Active, rev.Status);
        Assert.Equal("my-tutor", rev.ActivationSlug);
        Assert.Equal("old-slug", rev.PreviousActivationSlug);
        Assert.NotNull(rev.ActivatedUtc);
    }

    [Fact]
    public void Activate_FromDraft_Throws()
    {
        var rev = NewDraft();
        Assert.Throws<InvalidOperationException>(() => rev.Activate(Guid.NewGuid(), "slug", null));
    }

    [Fact]
    public void Activate_WithoutSlug_Throws()
    {
        var rev = NewDraft();
        rev.Publish(Guid.NewGuid());
        Assert.Throws<ArgumentException>(() => rev.Activate(Guid.NewGuid(), "  ", null));
    }

    [Fact]
    public void Archive_FromActive_ClearsSlug()
    {
        var rev = NewDraft();
        rev.Publish(Guid.NewGuid());
        rev.Activate(Guid.NewGuid(), "slug", null);

        rev.Archive(Guid.NewGuid());

        Assert.Equal(RevisionStatus.Archived, rev.Status);
        Assert.Null(rev.ActivationSlug);
        Assert.NotNull(rev.ArchivedUtc);
    }

    [Fact]
    public void Archive_FromPublished_Succeeds()
    {
        var rev = NewDraft();
        rev.Publish(Guid.NewGuid());

        rev.Archive(Guid.NewGuid());

        Assert.Equal(RevisionStatus.Archived, rev.Status);
    }

    [Fact]
    public void Archive_FromDraft_Throws()
    {
        var rev = NewDraft();
        Assert.Throws<InvalidOperationException>(() => rev.Archive(Guid.NewGuid()));
    }
}
