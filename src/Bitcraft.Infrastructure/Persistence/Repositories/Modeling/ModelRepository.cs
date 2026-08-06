using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Modeling;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.Infrastructure.Persistence.Repositories.Modeling;

public class ModelRepository(AppDbContext db) : IModelRepository
{
    public Task<Model?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Models.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var query = db.Models.Where(x => x.Name == name);
        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }
        return query.AnyAsync(cancellationToken);
    }

    public Task<InteractionModel?> GetInteractionModelByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.InteractionModels.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<InteractionModel>> ListInteractionModelsAsync(CancellationToken cancellationToken = default) =>
        db.InteractionModels.OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public async Task<InteractionModel> AddAsync(InteractionModel model, CancellationToken cancellationToken = default)
    {
        db.InteractionModels.Add(model);
        await db.SaveChangesAsync(cancellationToken);
        return model;
    }

    public async Task UpdateAsync(InteractionModel model, CancellationToken cancellationToken = default)
    {
        db.InteractionModels.Update(model);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(InteractionModel model, CancellationToken cancellationToken = default)
    {
        db.InteractionModels.Remove(model);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<EmbeddingModel?> GetEmbeddingModelByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.EmbeddingModels.Include(x => x.RuntimeEngine).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<EmbeddingModel>> ListEmbeddingModelsAsync(CancellationToken cancellationToken = default) =>
        db.EmbeddingModels.OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public async Task<EmbeddingModel> AddAsync(EmbeddingModel model, CancellationToken cancellationToken = default)
    {
        db.EmbeddingModels.Add(model);
        await db.SaveChangesAsync(cancellationToken);
        return model;
    }

    public async Task UpdateAsync(EmbeddingModel model, CancellationToken cancellationToken = default)
    {
        db.EmbeddingModels.Update(model);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(EmbeddingModel model, CancellationToken cancellationToken = default)
    {
        db.EmbeddingModels.Remove(model);
        await db.SaveChangesAsync(cancellationToken);
    }
}
