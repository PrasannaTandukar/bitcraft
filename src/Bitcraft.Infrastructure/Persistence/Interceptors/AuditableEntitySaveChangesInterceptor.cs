using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bitcraft.Infrastructure.Persistence.Interceptors;

public class AuditableEntitySaveChangesInterceptor(ICurrentUserContext currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAudit(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var userId = currentUser.UserId;
        foreach (var entry in context.ChangeTracker.Entries<BaseModel>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.UpdateMetadata(userId);
                continue;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdateMetadata(userId);
            }
        }
    }
}
