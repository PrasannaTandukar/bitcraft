using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Modeling;

namespace Bitcraft.UnitTests.Application.Admin.Fakes;

internal sealed class FakeRuntimeEngineRepository : IRuntimeEngineRepository
{
    public List<RuntimeEngine> Engines { get; } = new();
    public int AddCalls { get; private set; }
    public int UpdateCalls { get; private set; }
    public int DeleteCalls { get; private set; }

    public Task<RuntimeEngine> AddAsync(RuntimeEngine engine, CancellationToken cancellationToken = default)
    {
        AddCalls++;
        Engines.Add(engine);
        return Task.FromResult(engine);
    }

    public Task<RuntimeEngine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Engines.FirstOrDefault(e => e.Id == id));

    public Task<RuntimeEngine?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(Engines.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal)));

    public Task<List<RuntimeEngine>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Engines.OrderBy(e => e.Name).ToList());

    public Task UpdateAsync(RuntimeEngine engine, CancellationToken cancellationToken = default)
    {
        UpdateCalls++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(RuntimeEngine engine, CancellationToken cancellationToken = default)
    {
        DeleteCalls++;
        Engines.Remove(engine);
        return Task.CompletedTask;
    }
}

internal sealed class FakeRuntimeModelDiagnostic : IRuntimeModelDiagnostic
{
    public int ProbeCalls { get; private set; }
    public string? LastEndpoint { get; private set; }
    public string? LastApiKey { get; private set; }
    public RuntimeEngineProviderType? LastProviderType { get; private set; }
    public IReadOnlyList<string>? ProbeResult { get; set; } = ["model-a", "model-b"];

    public Task<Result<RuntimeModelCatalogDiagnostic>> ProbeModelsAsync(
        RuntimeModelCatalogDiagnosticRequest request,
        CancellationToken cancellationToken = default)
    {
        ProbeCalls++;
        LastEndpoint = request.Endpoint;
        LastApiKey = request.ApiKey;
        LastProviderType = request.ProviderType;
        return Task.FromResult(Result.Success(new RuntimeModelCatalogDiagnostic(ProbeResult ?? [])));
    }

    public Task<Result<RuntimeModelGenerationDiagnostic>> GenerateAsync(
        RuntimeModelGenerationDiagnosticRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure<RuntimeModelGenerationDiagnostic>(new Error("Fake.NotSupported", "Generation is not used in these tests.")));
}
