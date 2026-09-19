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

internal sealed class FakeLlmGenerationProvider : ILlmGenerationProvider
{
    public int ProbeCalls { get; private set; }
    public string? LastEndpoint { get; private set; }
    public string? LastApiKey { get; private set; }
    public string? LastProviderType { get; private set; }
    public IReadOnlyList<string>? ProbeResult { get; set; } = ["model-a", "model-b"];

    public Task<Result<LlmGenerationResult>> GenerateAsync(
        string prompt,
        string? model,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure<LlmGenerationResult>(new Error("Fake.NotSupported", "Generation is not used in these tests.")));

    public Task<IReadOnlyList<string>?> ProbeAvailableModelsAsync(
        string endpoint,
        string? ApiKey,
        string? providerType,
        CancellationToken cancellationToken = default)
    {
        ProbeCalls++;
        LastEndpoint = endpoint;
        LastApiKey = ApiKey;
        LastProviderType = providerType;
        return Task.FromResult(ProbeResult);
    }
}
