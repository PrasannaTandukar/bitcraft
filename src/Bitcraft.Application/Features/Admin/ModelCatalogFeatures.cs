using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Admin;

public static class ModelCatalogFeatures
{
    public sealed record CreateModelCatalogEntryCommand(
        string Name,
        ModelProviderType ProviderType,
        string Endpoint,
        string ModelName,
        string? ApiKeySecretRef,
        string? SettingsJson,
        bool IsEnabled) : IRequest<Result<ModelCatalogEntry>>;

    public sealed class CreateModelCatalogEntryHandler(
        IModelCatalogRepository repository,
        ICurrentUserContext currentUser)
        : IRequestHandler<CreateModelCatalogEntryCommand, Result<ModelCatalogEntry>>
    {
        public async Task<Result<ModelCatalogEntry>> Handle(CreateModelCatalogEntryCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole("Admin"))
            {
                return Result.Failure<ModelCatalogEntry>(new Error("ModelCatalog.Forbidden", "Only administrators can manage model catalog entries."));
            }

            var validation = ValidateCatalogEntry(request.Name, request.Endpoint, request.ModelName);
            if (validation is not null)
            {
                return Result.Failure<ModelCatalogEntry>(validation);
            }

            var entry = new ModelCatalogEntry
            {
                Name = request.Name.Trim(),
                ProviderType = request.ProviderType,
                Endpoint = request.Endpoint.Trim(),
                ModelName = request.ModelName.Trim(),
                ApiKeySecretRef = string.IsNullOrWhiteSpace(request.ApiKeySecretRef) ? null : request.ApiKeySecretRef.Trim(),
                SettingsJson = request.SettingsJson,
                IsEnabled = request.IsEnabled
            };

            var created = await repository.AddAsync(entry, cancellationToken);
            return Result.Success(created);
        }
    }

    public sealed record ListModelCatalogEntriesQuery : IRequest<Result<List<ModelCatalogEntry>>>;

    public sealed class ListModelCatalogEntriesHandler(
        IModelCatalogRepository repository,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListModelCatalogEntriesQuery, Result<List<ModelCatalogEntry>>>
    {
        public async Task<Result<List<ModelCatalogEntry>>> Handle(ListModelCatalogEntriesQuery request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole("Admin"))
            {
                return Result.Failure<List<ModelCatalogEntry>>(new Error("ModelCatalog.Forbidden", "Only administrators can view model catalog entries."));
            }

            var entries = await repository.ListAsync(cancellationToken);
            return Result.Success(entries);
        }
    }

    public sealed record CheckModelCatalogEntryHealthQuery(Guid ModelCatalogEntryId) : IRequest<Result<ModelHealthStatus>>;

    public sealed class CheckModelCatalogEntryHealthHandler(
        IModelCatalogRepository repository,
        IModelConnectionHealthCheck healthCheck,
        ICurrentUserContext currentUser)
        : IRequestHandler<CheckModelCatalogEntryHealthQuery, Result<ModelHealthStatus>>
    {
        public async Task<Result<ModelHealthStatus>> Handle(CheckModelCatalogEntryHealthQuery request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole("Admin"))
            {
                return Result.Failure<ModelHealthStatus>(new Error("ModelCatalog.Forbidden", "Only administrators can probe model catalog entries."));
            }

            var entry = await repository.GetByIdAsync(request.ModelCatalogEntryId, cancellationToken);
            if (entry is null)
            {
                return Result.Failure<ModelHealthStatus>(new Error("ModelCatalog.NotFound", $"Model catalog entry '{request.ModelCatalogEntryId}' not found."));
            }

            return await healthCheck.CheckAsync(entry, cancellationToken);
        }
    }

    private static Error? ValidateCatalogEntry(string name, string endpoint, string modelName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new Error("ModelCatalog.NameRequired", "Catalog entry name is required.");
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new Error("ModelCatalog.EndpointRequired", "Endpoint is required.");
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new Error("ModelCatalog.EndpointInvalid", "Endpoint must be a valid http or https URI.");
        }

        if (string.IsNullOrWhiteSpace(modelName))
        {
            return new Error("ModelCatalog.ModelRequired", "Model name is required.");
        }

        return null;
    }
}
