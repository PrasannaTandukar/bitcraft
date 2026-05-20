using Bitcraft.Domain.Common;
using Bitcraft.Domain.Benchmarking;
using Bitcraft.Domain.Identity;

namespace Bitcraft.Domain.Tutoring;

/// <summary>
/// Tutor aggregate root. Ownership and access are managed through the <see cref="Members"/>
/// collection (resource membership pattern, D2/D3) rather than a single owner column.
/// </summary>
public class Tutor : BaseModel
{
    /// <summary>Tutor display name (required, ≤200 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional human-readable description.</summary>
    public string? Description { get; set; }

    /// <summary>Lifecycle status. Defaults to <see cref="TutorStatus.Draft"/>.</summary>
    public TutorStatus Status { get; set; } = TutorStatus.Draft;

    /// <summary>Currently active prompt version (nullable until first version is promoted).</summary>
    public Guid? ActivePromptVersionId { get; set; }

    /// <summary>Currently active <see cref="TutorRevision"/> (D25 step 7, plan.erm.md §7.1). Mirrors the row whose Status is Active.</summary>
    public Guid? ActiveRevisionId { get; set; }
    public TutorRevision? ActiveRevision { get; set; }

    public TutorModelConnection? TutorModelConnection { get; set; }
    public PromptVersion? ActivePromptVersion { get; set; }
    public DeploymentProfile? DeploymentProfile { get; set; }
    public ICollection<PromptVersion> PromptVersions { get; set; } = new List<PromptVersion>();
    public ICollection<KnowledgeSourceLink> KnowledgeSources { get; set; } = new List<KnowledgeSourceLink>();
    public ICollection<TutorDocument> TutorDocuments { get; set; } = new List<TutorDocument>();
    public ICollection<TutorDocumentFolder> TutorDocumentFolders { get; set; } = new List<TutorDocumentFolder>();
    public ICollection<TutorRevision> Revisions { get; set; } = new List<TutorRevision>();
    public ICollection<ToolDefinition> Tools { get; set; } = new List<ToolDefinition>();
    public ICollection<TutorConfigurationSnapshot> Snapshots { get; set; } = new List<TutorConfigurationSnapshot>();
    public ICollection<BenchmarkRun> BenchmarkRuns { get; set; } = new List<BenchmarkRun>();

    /// <summary>
    /// Resource-scoped membership rows for this tutor. The "≥1 Owner at all times" invariant
    /// (plan.erm.md §3.2) is enforced at the Application layer via <see cref="WouldLeaveNoOwner"/>.
    /// </summary>
    public ICollection<TutorMember> Members { get; set; } = new List<TutorMember>();

    /// <summary>
    /// Construct a draft tutor with a founding <see cref="ResourceRole.Owner"/> membership for
    /// <paramref name="foundingOwnerUserId"/>. The founding membership is recorded as granted by
    /// the same user (self-grant on creation).
    /// </summary>
    /// <param name="name">Display name. Caller is responsible for trimming/validating.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="foundingOwnerUserId">User id of the initial sole Owner.</param>
    public static Tutor Create(string name, string? description, Guid foundingOwnerUserId)
    {
        var tutor = new Tutor
        {
            Name = name,
            Description = description,
            Status = TutorStatus.Draft,
        };

        tutor.Members.Add(new TutorMember
        {
            TutorId = tutor.Id,
            UserId = foundingOwnerUserId,
            Role = ResourceRole.Owner,
            GrantedById = foundingOwnerUserId,
            GrantedUtc = DateTime.UtcNow,
        });

        return tutor;
    }

    /// <summary>
    /// Returns true when removing or demoting the membership identified by <paramref name="memberId"/>
    /// would leave this tutor with zero <see cref="ResourceRole.Owner"/> members. Application
    /// handlers MUST call this before any revoke/demote operation (≥1 Owner invariant).
    /// </summary>
    public bool WouldLeaveNoOwner(Guid memberId)
    {
        var target = Members.FirstOrDefault(m => m.Id == memberId);
        if (target is null || target.Role != ResourceRole.Owner)
        {
            return false;
        }

        return Members.Count(m => m.Role == ResourceRole.Owner) <= 1;
    }
}

/// <summary>
/// Resource-membership row binding an <see cref="AppUser"/> to a <see cref="Tutor"/> with a
/// <see cref="ResourceRole"/>. Aggregate-local to <see cref="Tutor"/>; per plan.erm.md §3.2 a
/// sibling <c>DocumentFolderMember</c> may follow the same shape later.
/// </summary>
public class TutorMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Owning tutor (FK, cascade-delete with parent tutor).</summary>
    public Guid TutorId { get; set; }
    public Tutor? Tutor { get; set; }

    /// <summary>Subject user holding the membership (FK, restrict-delete — plan.erm.md §6).</summary>
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    /// <summary>Resource-scoped role on the owning tutor.</summary>
    public ResourceRole Role { get; set; }

    /// <summary>UTC instant the membership was granted.</summary>
    public DateTime GrantedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>User who granted this membership (FK, restrict-delete).</summary>
    public Guid GrantedById { get; set; }
    public AppUser? GrantedBy { get; set; }
}
