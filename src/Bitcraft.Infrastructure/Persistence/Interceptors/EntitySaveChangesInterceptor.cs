using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bitcraft.Infrastructure.Persistence.Interceptors;

public class EntitySaveChangesInterceptor(ICurrentUserContext currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        SetEditor(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SetEditor(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void SetEditor(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var userId = currentUser.UserIdOrNull ?? Guid.Empty;
        foreach (var entry in context.ChangeTracker.Entries<BaseModel>())
        {
            if (entry.State == EntityState.Added)
            {
                var creatorId = currentUser.UserIdOrNull ?? entry.Entity.CreatedById;
                if (creatorId != Guid.Empty)
                {
                    entry.Entity.UpdateMetadata(creatorId);
                }

                continue;
            }

            if (entry.State == EntityState.Modified)
            {
                var userId = currentUser.UserIdOrNull;
                if (userId is null || userId == Guid.Empty)
                {
                    continue;
                }

                entry.Entity.UpdateMetadata(userId.Value);
            }
        }
    }
}
