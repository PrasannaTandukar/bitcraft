using Bitcraft.Domain.Tutoring;

namespace Bitcraft.Application.Common.Interfaces;

/// <summary>
/// Read-only abstraction over <see cref="TutorMember"/> rows. Application handlers use this to
/// authorize tutor-scoped actions without taking a direct dependency on EF or the Tutor aggregate's
/// in-memory <c>Members</c> graph. (D2/D3 resource membership pattern.)
/// </summary>
public interface ITutorMembershipReader
{
    /// <summary>
    /// Returns the highest <see cref="ResourceRole"/> the user holds on the given tutor, or
    /// <see langword="null"/> when the user has no membership.
    /// </summary>
    /// <param name="tutorId">Tutor identifier.</param>
    /// <param name="userId">Subject user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ResourceRole?> GetRoleAsync(Guid tutorId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every membership row for the given tutor. Used by grant/revoke flows that must
    /// inspect Owner counts before mutating.
    /// </summary>
    Task<IReadOnlyList<TutorMember>> ListMembersAsync(Guid tutorId, CancellationToken cancellationToken = default);
}
