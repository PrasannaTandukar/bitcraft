using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Modeling;
using MediatR;

namespace Bitcraft.Application.Features.Admin;

public static class EmbeddingModelFeatures
{
    private const int MaxNameLength = 200;
    private const int MaxProviderModelNameLength = 200;
    private const int MinEmbeddingDimensions = 1;
    private const int MaxEmbeddingDimensions = 16384;

    public sealed record CreateEmbeddingModelCommand(
        string Name,
        Guid RuntimeEngineId,
        string ProviderModelName,
        int EmbeddingDimensions,
        string? SettingsJson,
        bool IsEnabled) : IRequest<Result<EmbeddingModel>>;

    public sealed class CreateEmbeddingModelHandler(
        IModelRepository models,
        IRuntimeEngineRepository engines,
        ICurrentUserContext currentUser)
        : IRequestHandler<CreateEmbeddingModelCommand, Result<EmbeddingModel>>
    {
        public async Task<Result<EmbeddingModel>> Handle(CreateEmbeddingModelCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<EmbeddingModel>(new Error("EmbeddingModel.Forbidden", "Only administrators can manage embedding models."));
            }

            var validation = Validate(request.Name, request.ProviderModelName, request.EmbeddingDimensions);
            if (validation is not null)
            {
                return Result.Failure<EmbeddingModel>(validation);
            }

            var engine = await engines.GetByIdAsync(request.RuntimeEngineId, cancellationToken);
            if (engine is null)
            {
                return Result.Failure<EmbeddingModel>(new Error("RuntimeEngine.NotFound", $"Runtime engine '{request.RuntimeEngineId}' not found."));
            }

            if (!engine.IsEnabled)
            {
                return Result.Failure<EmbeddingModel>(new Error("RuntimeEngine.Disabled", "Selected runtime engine is disabled."));
            }

            var trimmedName = request.Name.Trim();
            if (await models.NameExistsAsync(trimmedName, excludeId: null, cancellationToken))
            {
                return Result.Failure<EmbeddingModel>(new Error("Model.NameConflict", "A model with that name already exists."));
            }

            var model = new EmbeddingModel
            {
                Name = trimmedName,
                RuntimeEngineId = request.RuntimeEngineId,
                ProviderModelName = request.ProviderModelName.Trim(),
                EmbeddingDimensions = request.EmbeddingDimensions,
                SettingsJson = string.IsNullOrWhiteSpace(request.SettingsJson) ? null : request.SettingsJson,
                IsEnabled = request.IsEnabled
            };

            var created = await models.AddAsync(model, cancellationToken);
            return Result.Success(created);
        }
    }

    public sealed record ListEmbeddingModelsQuery : IRequest<Result<List<EmbeddingModel>>>;

    public sealed class ListEmbeddingModelsHandler(
        IModelRepository models,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListEmbeddingModelsQuery, Result<List<EmbeddingModel>>>
    {
        public async Task<Result<List<EmbeddingModel>>> Handle(ListEmbeddingModelsQuery request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<List<EmbeddingModel>>(new Error("EmbeddingModel.Forbidden", "Only administrators can view embedding models."));
            }

            var list = await models.ListEmbeddingModelsAsync(cancellationToken);
            return Result.Success(list);
        }
    }

    public sealed record GetEmbeddingModelQuery(Guid Id) : IRequest<Result<EmbeddingModel>>;

    public sealed class GetEmbeddingModelHandler(
        IModelRepository models,
        ICurrentUserContext currentUser)
        : IRequestHandler<GetEmbeddingModelQuery, Result<EmbeddingModel>>
    {
        public async Task<Result<EmbeddingModel>> Handle(GetEmbeddingModelQuery request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<EmbeddingModel>(new Error("EmbeddingModel.Forbidden", "Only administrators can view embedding models."));
            }

            var model = await models.GetEmbeddingModelByIdAsync(request.Id, cancellationToken);
            if (model is null)
            {
                return Result.Failure<EmbeddingModel>(new Error("EmbeddingModel.NotFound", $"Embedding model '{request.Id}' not found."));
            }

            return Result.Success(model);
        }
    }

    public sealed record UpdateEmbeddingModelCommand(
        Guid Id,
        string Name,
        Guid RuntimeEngineId,
        string ProviderModelName,
        int EmbeddingDimensions,
        string? SettingsJson,
        bool IsEnabled) : IRequest<Result<EmbeddingModel>>;

    public sealed class UpdateEmbeddingModelHandler(
        IModelRepository models,
        IRuntimeEngineRepository engines,
        ICurrentUserContext currentUser)
        : IRequestHandler<UpdateEmbeddingModelCommand, Result<EmbeddingModel>>
    {
        public async Task<Result<EmbeddingModel>> Handle(UpdateEmbeddingModelCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<EmbeddingModel>(new Error("EmbeddingModel.Forbidden", "Only administrators can manage embedding models."));
            }

            var validation = Validate(request.Name, request.ProviderModelName, request.EmbeddingDimensions);
            if (validation is not null)
            {
                return Result.Failure<EmbeddingModel>(validation);
            }

            var existing = await models.GetEmbeddingModelByIdAsync(request.Id, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<EmbeddingModel>(new Error("EmbeddingModel.NotFound", $"Embedding model '{request.Id}' not found."));
            }

            if (existing.EmbeddingDimensions != request.EmbeddingDimensions)
            {
                return Result.Failure<EmbeddingModel>(new Error(
                    "EmbeddingModel.DimensionsImmutable",
                    "Embedding dimensions cannot be changed; create a new embedding model instead."));
            }

            var engine = await engines.GetByIdAsync(request.RuntimeEngineId, cancellationToken);
            if (engine is null)
            {
                return Result.Failure<EmbeddingModel>(new Error("RuntimeEngine.NotFound", $"Runtime engine '{request.RuntimeEngineId}' not found."));
            }

            if (!engine.IsEnabled)
            {
                return Result.Failure<EmbeddingModel>(new Error("RuntimeEngine.Disabled", "Selected runtime engine is disabled."));
            }

            var trimmedName = request.Name.Trim();
            if (await models.NameExistsAsync(trimmedName, excludeId: existing.Id, cancellationToken))
            {
                return Result.Failure<EmbeddingModel>(new Error("Model.NameConflict", "A model with that name already exists."));
            }

            existing.Name = trimmedName;
            existing.RuntimeEngineId = request.RuntimeEngineId;
            existing.ProviderModelName = request.ProviderModelName.Trim();
            existing.SettingsJson = string.IsNullOrWhiteSpace(request.SettingsJson) ? null : request.SettingsJson;
            existing.IsEnabled = request.IsEnabled;

            await models.UpdateAsync(existing, cancellationToken);
            return Result.Success(existing);
        }
    }

    public sealed record DeleteEmbeddingModelCommand(Guid Id) : IRequest<Result>;

    public sealed class DeleteEmbeddingModelHandler(
        IModelRepository models,
        ICurrentUserContext currentUser)
        : IRequestHandler<DeleteEmbeddingModelCommand, Result>
    {
        public async Task<Result> Handle(DeleteEmbeddingModelCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure(new Error("EmbeddingModel.Forbidden", "Only administrators can manage embedding models."));
            }

            var existing = await models.GetEmbeddingModelByIdAsync(request.Id, cancellationToken);
            if (existing is null)
            {
                return Result.Failure(new Error("EmbeddingModel.NotFound", $"Embedding model '{request.Id}' not found."));
            }

            await models.DeleteAsync(existing, cancellationToken);
            return Result.Success();
        }
    }

    private static Error? Validate(string name, string providerModelName, int dimensions)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new Error("EmbeddingModel.NameRequired", "Model name is required.");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            return new Error("EmbeddingModel.NameTooLong", $"Model name cannot exceed {MaxNameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(providerModelName))
        {
            return new Error("EmbeddingModel.ProviderModelRequired", "Provider model name is required.");
        }

        if (providerModelName.Trim().Length > MaxProviderModelNameLength)
        {
            return new Error("EmbeddingModel.ProviderModelTooLong", $"Provider model name cannot exceed {MaxProviderModelNameLength} characters.");
        }

        if (dimensions is < MinEmbeddingDimensions or > MaxEmbeddingDimensions)
        {
            return new Error("EmbeddingModel.DimensionsInvalid", $"Embedding dimensions must be between {MinEmbeddingDimensions} and {MaxEmbeddingDimensions}.");
        }

        return null;
    }
}
