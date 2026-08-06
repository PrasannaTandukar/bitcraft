using Bitcraft.Domain.Modeling;

namespace Bitcraft.Application.Common.Interfaces;

/// <summary>
/// Repository over the admin-managed Model TPH hierarchy (<see cref="InteractionModel"/>, <see cref="EmbeddingModel"/>).
/// Type-specific queries enable role-aware admin UI and tutor selection without TPH leakage outside Infrastructure.
/// </summary>
public interface IModelRepository
{
    // Cross-type lookups
    Task<Model?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default);

    // InteractionModel
    Task<InteractionModel?> GetInteractionModelByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<InteractionModel>> ListInteractionModelsAsync(CancellationToken cancellationToken = default);
    Task<InteractionModel> AddAsync(InteractionModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(InteractionModel model, CancellationToken cancellationToken = default);
    Task DeleteAsync(InteractionModel model, CancellationToken cancellationToken = default);

    // EmbeddingModel
    Task<EmbeddingModel?> GetEmbeddingModelByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<EmbeddingModel>> ListEmbeddingModelsAsync(CancellationToken cancellationToken = default);
    Task<EmbeddingModel> AddAsync(EmbeddingModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(EmbeddingModel model, CancellationToken cancellationToken = default);
    Task DeleteAsync(EmbeddingModel model, CancellationToken cancellationToken = default);
}
