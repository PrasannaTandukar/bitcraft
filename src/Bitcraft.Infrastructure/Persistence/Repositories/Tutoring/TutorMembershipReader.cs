using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Tutoring;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories.Tutoring;

/// <summary>
/// EF-backed implementation of <see cref="ITutorMembershipReader"/>. Queries <c>TutorMembers</c>
/// rows directly; never loads the full <see cref="Tutor"/> aggregate.
/// </summary>
public class TutorMembershipReader(AppDbContext db) : ITutorMembershipReader
{
    public async Task<ResourceRole?> GetRoleAsync(Guid tutorId, Guid userId, CancellationToken cancellationToken = default)
    {
        // Pick the highest role for this (tutor, user). Unique index makes this single-row,
        // but Max() keeps the contract robust if invariants are ever loosened.
        var rows = await db.Set<TutorMember>()
            .Where(m => m.TutorId == tutorId && m.UserId == userId)
            .Select(m => (ResourceRole?)m.Role)
            .ToListAsync(cancellationToken);

        return rows.Count == 0 ? null : rows.Max();
    }

    public async Task<IReadOnlyList<TutorMember>> ListMembersAsync(Guid tutorId, CancellationToken cancellationToken = default) =>
        await db.Set<TutorMember>()
            .AsNoTracking()
            .Where(m => m.TutorId == tutorId)
            .ToListAsync(cancellationToken);
}
