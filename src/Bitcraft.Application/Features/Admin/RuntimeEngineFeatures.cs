using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Modeling;
using MediatR;

namespace Bitcraft.Application.Features.Admin;

/// <summary>
/// Admin-only CRUD for the <see cref="RuntimeEngine"/> aggregate (D11 / D25 step 3).
/// </summary>
public static class RuntimeEngineFeatures
{
    // ----- Create -----

    public sealed record CreateRuntimeEngineCommand(
        string Name,
        RuntimeEngineProviderType ProviderType,
        string Endpoint,
        string? ApiKeySecretRef,
        bool IsEnabled) : IRequest<Result<RuntimeEngine>>;

    public sealed class CreateRuntimeEngineHandler(
        IRuntimeEngineRepository repository,
        ICurrentUserContext currentUser)
        : IRequestHandler<CreateRuntimeEngineCommand, Result<RuntimeEngine>>
    {
        public async Task<Result<RuntimeEngine>> Handle(CreateRuntimeEngineCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<RuntimeEngine>(new Error("RuntimeEngine.Forbidden", "Only administrators can manage runtime engines."));
            }

            var validation = Validate(request.Name, request.ProviderType, request.Endpoint, request.ApiKeySecretRef);
            if (validation is not null)
            {
                return Result.Failure<RuntimeEngine>(validation);
            }

            var name = request.Name.Trim();
            var existing = await repository.GetByNameAsync(name, cancellationToken);
            if (existing is not null)
            {
                return Result.Failure<RuntimeEngine>(new Error("RuntimeEngine.NameConflict", $"A runtime engine named '{name}' already exists."));
            }

            var engine = new RuntimeEngine
            {
                Name = name,
                ProviderType = request.ProviderType,
                Endpoint = request.Endpoint.Trim(),
                ApiKeySecretRef = string.IsNullOrWhiteSpace(request.ApiKeySecretRef) ? null : request.ApiKeySecretRef.Trim(),
                IsEnabled = request.IsEnabled
            };

            var created = await repository.AddAsync(engine, cancellationToken);
            return Result.Success(created);
        }
    }

    // ----- List -----

    public sealed record ListRuntimeEnginesQuery : IRequest<Result<List<RuntimeEngine>>>;

    public sealed class ListRuntimeEnginesHandler(
        IRuntimeEngineRepository repository,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListRuntimeEnginesQuery, Result<List<RuntimeEngine>>>
    {
        public async Task<Result<List<RuntimeEngine>>> Handle(ListRuntimeEnginesQuery request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<List<RuntimeEngine>>(new Error("RuntimeEngine.Forbidden", "Only administrators can view runtime engines."));
            }

            var engines = await repository.ListAsync(cancellationToken);
            return Result.Success(engines);
        }
    }

    // ----- Get -----

    public sealed record GetRuntimeEngineQuery(Guid RuntimeEngineId) : IRequest<Result<RuntimeEngine>>;

    public sealed class GetRuntimeEngineHandler(
        IRuntimeEngineRepository repository,
        ICurrentUserContext currentUser)
        : IRequestHandler<GetRuntimeEngineQuery, Result<RuntimeEngine>>
    {
        public async Task<Result<RuntimeEngine>> Handle(GetRuntimeEngineQuery request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<RuntimeEngine>(new Error("RuntimeEngine.Forbidden", "Only administrators can view runtime engines."));
            }

            var engine = await repository.GetByIdAsync(request.RuntimeEngineId, cancellationToken);
            if (engine is null)
            {
                return Result.Failure<RuntimeEngine>(new Error("RuntimeEngine.NotFound", $"Runtime engine '{request.RuntimeEngineId}' not found."));
            }

            return Result.Success(engine);
        }
    }

    // ----- Update -----

    /// <summary>
    /// Updates an admin-managed runtime engine. A null or empty <paramref name="ApiKeySecretRef"/>
    /// preserves the stored key reference. Set <paramref name="ClearApiKeySecret"/> to remove it.
    /// </summary>
    public sealed record UpdateRuntimeEngineCommand(
        Guid RuntimeEngineId,
        string Name,
        RuntimeEngineProviderType ProviderType,
        string Endpoint,
        string? ApiKeySecretRef,
        bool IsEnabled,
        bool ClearApiKeySecret = false) : IRequest<Result<RuntimeEngine>>;

    public sealed class UpdateRuntimeEngineHandler(
        IRuntimeEngineRepository repository,
        ICurrentUserContext currentUser)
        : IRequestHandler<UpdateRuntimeEngineCommand, Result<RuntimeEngine>>
    {
        public async Task<Result<RuntimeEngine>> Handle(UpdateRuntimeEngineCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<RuntimeEngine>(new Error("RuntimeEngine.Forbidden", "Only administrators can manage runtime engines."));
            }

            var validation = Validate(request.Name, request.ProviderType, request.Endpoint, request.ApiKeySecretRef);
            if (validation is not null)
            {
                return Result.Failure<RuntimeEngine>(validation);
            }

            var engine = await repository.GetByIdAsync(request.RuntimeEngineId, cancellationToken);
            if (engine is null)
            {
                return Result.Failure<RuntimeEngine>(new Error("RuntimeEngine.NotFound", $"Runtime engine '{request.RuntimeEngineId}' not found."));
            }

            var name = request.Name.Trim();
            if (!string.Equals(engine.Name, name, StringComparison.Ordinal))
            {
                var clash = await repository.GetByNameAsync(name, cancellationToken);
                if (clash is not null && clash.Id != engine.Id)
                {
                    return Result.Failure<RuntimeEngine>(new Error("RuntimeEngine.NameConflict", $"A runtime engine named '{name}' already exists."));
                }
            }

            engine.Name = name;
            engine.ProviderType = request.ProviderType;
            engine.Endpoint = request.Endpoint.Trim();
            // Preserve-on-null: a blank key reference from the client keeps the stored value.
            // Removal is explicit via ClearApiKeySecret so an edit-save never silently drops the key.
            if (request.ClearApiKeySecret)
            {
                engine.ApiKeySecretRef = null;
            }
            else if (!string.IsNullOrWhiteSpace(request.ApiKeySecretRef))
            {
                engine.ApiKeySecretRef = request.ApiKeySecretRef.Trim();
            }
            engine.IsEnabled = request.IsEnabled;

            await repository.UpdateAsync(engine, cancellationToken);
            return Result.Success(engine);
        }
    }

    // ----- Delete -----

    public sealed record DeleteRuntimeEngineCommand(Guid RuntimeEngineId) : IRequest<Result<bool>>;

    public sealed class DeleteRuntimeEngineHandler(
        IRuntimeEngineRepository repository,
        ICurrentUserContext currentUser)
        : IRequestHandler<DeleteRuntimeEngineCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(DeleteRuntimeEngineCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<bool>(new Error("RuntimeEngine.Forbidden", "Only administrators can manage runtime engines."));
            }

            var engine = await repository.GetByIdAsync(request.RuntimeEngineId, cancellationToken);
            if (engine is null)
            {
                return Result.Failure<bool>(new Error("RuntimeEngine.NotFound", $"Runtime engine '{request.RuntimeEngineId}' not found."));
            }

            // FK Restrict from Model.RuntimeEngineId (step 4) will block deletion at the database layer
            // once that FK is introduced. Step 3 has no FK from Model yet, so deletion is unconstrained here.
            await repository.DeleteAsync(engine, cancellationToken);
            return Result.Success(true);
        }
    }

    // ----- Test Connection -----

    public sealed record TestRuntimeEngineConnectionResult(
        bool IsSuccess,
        double LatencyMs,
        string Message,
        IReadOnlyList<string>? AvailableModels = null);

    public sealed record TestRuntimeEngineCommand(
        RuntimeEngineProviderType ProviderType,
        string Endpoint,
        string? ApiKeySecretRef,
        Guid? RuntimeEngineId = null) : IRequest<Result<TestRuntimeEngineConnectionResult>>;

    // Authorization is enforced by the API layer (ViewerOrAbove on the test endpoint). The handler
    // performs no role check so any authenticated user can run this read-only probe.
    public sealed class TestRuntimeEngineHandler(
        ILlmGenerationProvider generationProvider,
        IRuntimeEngineRepository repository)
        : IRequestHandler<TestRuntimeEngineCommand, Result<TestRuntimeEngineConnectionResult>>
    {
        public async Task<Result<TestRuntimeEngineConnectionResult>> Handle(
            TestRuntimeEngineCommand request,
            CancellationToken cancellationToken)
        {
            // Server-side key prefill: a probe without a client-supplied key reuses the stored
            // credential server-side so saved engines test with their persisted key. Read-only.
            // The stored key only attaches to the stored engine's own endpoint. Mixing a stored
            // credential with an arbitrary endpoint would let any authenticated user steer the
            // key to a host they control, so prefill requires an endpoint match.
            var apiKey = request.ApiKeySecretRef;
            if (string.IsNullOrWhiteSpace(apiKey) && request.RuntimeEngineId is { } engineId)
            {
                var stored = await repository.GetByIdAsync(engineId, cancellationToken);
                if (stored is not null &&
                    string.Equals(stored.Endpoint?.Trim(), request.Endpoint?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    apiKey = stored.ApiKeySecretRef;
                }
            }

            if (string.IsNullOrWhiteSpace(request.Endpoint) ||
                !Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return Result.Failure<TestRuntimeEngineConnectionResult>(
                    new Error("RuntimeEngine.EndpointInvalid", "Endpoint must be a valid http or https URI."));
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var discoveredModels = await generationProvider.ProbeAvailableModelsAsync(
                    request.Endpoint.Trim(),
                    string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim(),
                    request.ProviderType.ToString(),
                    cancellationToken);

                stopwatch.Stop();
                var latency = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1);

                if (discoveredModels is { Count: > 0 })
                {
                    return Result.Success(new TestRuntimeEngineConnectionResult(
                        true,
                        latency,
                        $"Successfully connected to {request.ProviderType} endpoint. Detected {discoveredModels.Count} available model(s).",
                        discoveredModels));
                }

                if (discoveredModels is not null)
                {
                    return Result.Success(new TestRuntimeEngineConnectionResult(
                        true,
                        latency,
                        $"Successfully reached {request.ProviderType} endpoint (no models reported).",
                        discoveredModels));
                }

                return Result.Success(new TestRuntimeEngineConnectionResult(
                    false,
                    latency,
                    $"Could not reach {request.ProviderType} endpoint or retrieve model catalog."));
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return Result.Success(new TestRuntimeEngineConnectionResult(
                    false,
                    Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1),
                    $"Connection failed: {ex.Message}"));
            }
        }
    }

    // ----- Validation -----

    private static Error? Validate(string name, RuntimeEngineProviderType providerType, string endpoint, string? apiKeySecretRef)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new Error("RuntimeEngine.NameRequired", "Runtime engine name is required.");
        }

        if (name.Trim().Length > 200)
        {
            return new Error("RuntimeEngine.NameTooLong", "Runtime engine name must be 200 characters or fewer.");
        }

        if (!Enum.IsDefined(providerType))
        {
            return new Error("RuntimeEngine.ProviderTypeInvalid", "Provider type is not a recognized value.");
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new Error("RuntimeEngine.EndpointRequired", "Endpoint is required.");
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new Error("RuntimeEngine.EndpointInvalid", "Endpoint must be a valid http or https URI.");
        }

        if (apiKeySecretRef is not null && apiKeySecretRef.Trim().Length > 200)
        {
            return new Error("RuntimeEngine.ApiKeySecretRefTooLong", "API key secret reference must be 200 characters or fewer.");
        }

        return null;
    }
}
