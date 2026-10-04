using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Tutoring;
using Microsoft.EntityFrameworkCore;
namespace Bitcraft.Infrastructure.Persistence;

public sealed class TutorConversationRepository(AppDbContext db) : ITutorConversationRepository
{
    public Task<TutorConversation?> GetAsync(Guid id, Guid userId, Guid tutorId, CancellationToken cancellationToken) =>
        // Owned knowledge/tool collections are eagerly loaded in a fixed number of queries,
        // avoiding multiplication of excerpt rows by tool calls and repeated answer/session text.
        db.Set<TutorConversation>().Include(value => value.Messages).AsSplitQuery().SingleOrDefaultAsync(
            value => value.Id == id && value.UserId == userId && value.TutorId == tutorId, cancellationToken);
    public async Task<IReadOnlyList<TutorConversation>> ListAsync(Guid userId, Guid tutorId, CancellationToken cancellationToken) =>
        await db.Set<TutorConversation>().AsNoTracking().Where(value => value.UserId == userId && value.TutorId == tutorId)
            .OrderByDescending(value => value.UpdatedUtc).Take(100).ToListAsync(cancellationToken);
    public async Task AddAsync(TutorConversation conversation, CancellationToken cancellationToken)
    {
        db.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task<bool> SaveAsync(TutorConversation conversation, CancellationToken cancellationToken)
    {
        // Advance the concurrency token first. PostgreSQL holds the row lock until both
        // the session update and normalized message inserts commit in this transaction.
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var previousVersion = db.Entry(conversation).Property(value => value.Version).OriginalValue;
        var added = conversation.Messages.Where(message => message.Sequence > previousVersion * 2).ToList();
        foreach (var message in added)
        {
            conversation.Messages.Remove(message);
            db.Entry(message).State = EntityState.Detached;
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            conversation.Messages.AddRange(added);
            db.AddRange(added);
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<bool> DeleteAsync(TutorConversation conversation, CancellationToken cancellationToken)
    {
        db.Remove(conversation);
        return await SaveAsync(conversation, cancellationToken);
    }
}
