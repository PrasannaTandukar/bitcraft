using Bitcraft.Application.Features.Admin;
using Bitcraft.Domain.Modeling;
using Bitcraft.UnitTests.Application.Admin.Fakes;
using Bitcraft.UnitTests.Application.Tutoring.Fakes;
using Xunit;

namespace Bitcraft.UnitTests.Application.Admin;

public class RuntimeEngineFeaturesTests
{
    private static FakeCurrentUserContext Admin() => new(Guid.NewGuid(), isAdmin: true);
    private static FakeCurrentUserContext NonAdmin() => new(Guid.NewGuid(), isAdmin: false);

    // ----- Create -----

    [Fact]
    public async Task Create_AsAdmin_PersistsEngine()
    {
        var repo = new FakeRuntimeEngineRepository();
        var handler = new RuntimeEngineFeatures.CreateRuntimeEngineHandler(repo, Admin());
        var cmd = new RuntimeEngineFeatures.CreateRuntimeEngineCommand(
            "Local Ollama",
            RuntimeEngineProviderType.Ollama,
            "http://localhost:11434",
            ApiKeySecretRef: null,
            IsEnabled: true);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repo.AddCalls);
        Assert.Single(repo.Engines);
        Assert.Equal("Local Ollama", result.Value.Name);
        Assert.Equal(RuntimeEngineProviderType.Ollama, result.Value.ProviderType);
        Assert.True(result.Value.IsEnabled);
    }

    [Fact]
    public async Task Create_AsNonAdmin_ReturnsForbidden()
    {
        var repo = new FakeRuntimeEngineRepository();
        var handler = new RuntimeEngineFeatures.CreateRuntimeEngineHandler(repo, NonAdmin());
        var cmd = new RuntimeEngineFeatures.CreateRuntimeEngineCommand(
            "Engine", RuntimeEngineProviderType.Ollama, "http://localhost:11434", null, true);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("RuntimeEngine.Forbidden", result.Error.Code);
        Assert.Equal(0, repo.AddCalls);
    }

    [Theory]
    [InlineData("", "http://localhost:11434", "RuntimeEngine.NameRequired")]
    [InlineData("Engine", "", "RuntimeEngine.EndpointRequired")]
    [InlineData("Engine", "ftp://example.com", "RuntimeEngine.EndpointInvalid")]
    [InlineData("Engine", "not-a-uri", "RuntimeEngine.EndpointInvalid")]
    public async Task Create_WithInvalidInput_ReturnsValidationError(string name, string endpoint, string expectedCode)
    {
        var repo = new FakeRuntimeEngineRepository();
        var handler = new RuntimeEngineFeatures.CreateRuntimeEngineHandler(repo, Admin());
        var cmd = new RuntimeEngineFeatures.CreateRuntimeEngineCommand(
            name, RuntimeEngineProviderType.Ollama, endpoint, null, true);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(0, repo.AddCalls);
    }

    [Fact]
    public async Task Create_WithDuplicateName_ReturnsConflict()
    {
        var repo = new FakeRuntimeEngineRepository();
        repo.Engines.Add(new RuntimeEngine { Name = "Ollama", ProviderType = RuntimeEngineProviderType.Ollama, Endpoint = "http://localhost:11434" });
        var handler = new RuntimeEngineFeatures.CreateRuntimeEngineHandler(repo, Admin());
        var cmd = new RuntimeEngineFeatures.CreateRuntimeEngineCommand(
            "Ollama", RuntimeEngineProviderType.Ollama, "http://localhost:11434", null, true);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("RuntimeEngine.NameConflict", result.Error.Code);
    }

    // ----- List / Get -----

    [Fact]
    public async Task List_AsAdmin_ReturnsAll()
    {
        var repo = new FakeRuntimeEngineRepository();
        repo.Engines.Add(new RuntimeEngine { Name = "B", Endpoint = "http://b" });
        repo.Engines.Add(new RuntimeEngine { Name = "A", Endpoint = "http://a" });

        var handler = new RuntimeEngineFeatures.ListRuntimeEnginesHandler(repo, Admin());
        var result = await handler.Handle(new RuntimeEngineFeatures.ListRuntimeEnginesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("A", result.Value[0].Name);
    }

    [Fact]
    public async Task List_AsNonAdmin_ReturnsForbidden()
    {
        var handler = new RuntimeEngineFeatures.ListRuntimeEnginesHandler(new FakeRuntimeEngineRepository(), NonAdmin());
        var result = await handler.Handle(new RuntimeEngineFeatures.ListRuntimeEnginesQuery(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("RuntimeEngine.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Get_WithUnknownId_ReturnsNotFound()
    {
        var handler = new RuntimeEngineFeatures.GetRuntimeEngineHandler(new FakeRuntimeEngineRepository(), Admin());
        var result = await handler.Handle(new RuntimeEngineFeatures.GetRuntimeEngineQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("RuntimeEngine.NotFound", result.Error.Code);
    }

    // ----- Update -----

    [Fact]
    public async Task Update_AsAdmin_AppliesChanges()
    {
        var repo = new FakeRuntimeEngineRepository();
        var engine = new RuntimeEngine { Name = "Old", ProviderType = RuntimeEngineProviderType.Ollama, Endpoint = "http://old" };
        repo.Engines.Add(engine);

        var handler = new RuntimeEngineFeatures.UpdateRuntimeEngineHandler(repo, Admin());
        var cmd = new RuntimeEngineFeatures.UpdateRuntimeEngineCommand(
            engine.Id, "New", RuntimeEngineProviderType.OpenAI, "https://new", "secret-ref", false);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repo.UpdateCalls);
        Assert.Equal("New", engine.Name);
        Assert.Equal(RuntimeEngineProviderType.OpenAI, engine.ProviderType);
        Assert.Equal("https://new", engine.Endpoint);
        Assert.Equal("secret-ref", engine.ApiKeySecretRef);
        Assert.False(engine.IsEnabled);
    }

    [Fact]
    public async Task Update_ToConflictingName_ReturnsConflict()
    {
        var repo = new FakeRuntimeEngineRepository();
        var other = new RuntimeEngine { Name = "Taken", Endpoint = "http://a" };
        var engine = new RuntimeEngine { Name = "Mine", Endpoint = "http://b" };
        repo.Engines.Add(other);
        repo.Engines.Add(engine);

        var handler = new RuntimeEngineFeatures.UpdateRuntimeEngineHandler(repo, Admin());
        var cmd = new RuntimeEngineFeatures.UpdateRuntimeEngineCommand(
            engine.Id, "Taken", RuntimeEngineProviderType.Ollama, "http://b", null, true);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("RuntimeEngine.NameConflict", result.Error.Code);
    }

    [Fact]
    public async Task Update_WithSameNameSameRow_Allowed()
    {
        var repo = new FakeRuntimeEngineRepository();
        var engine = new RuntimeEngine { Name = "Same", ProviderType = RuntimeEngineProviderType.Ollama, Endpoint = "http://x" };
        repo.Engines.Add(engine);

        var handler = new RuntimeEngineFeatures.UpdateRuntimeEngineHandler(repo, Admin());
        var cmd = new RuntimeEngineFeatures.UpdateRuntimeEngineCommand(
            engine.Id, "Same", RuntimeEngineProviderType.Ollama, "http://x", null, true);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    // ----- Delete -----

    [Fact]
    public async Task Delete_AsAdmin_Removes()
    {
        var repo = new FakeRuntimeEngineRepository();
        var engine = new RuntimeEngine { Name = "Doomed", Endpoint = "http://x" };
        repo.Engines.Add(engine);

        var handler = new RuntimeEngineFeatures.DeleteRuntimeEngineHandler(repo, Admin());
        var result = await handler.Handle(new RuntimeEngineFeatures.DeleteRuntimeEngineCommand(engine.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repo.DeleteCalls);
        Assert.Empty(repo.Engines);
    }

    [Fact]
    public async Task Delete_AsNonAdmin_ReturnsForbidden()
    {
        var repo = new FakeRuntimeEngineRepository();
        var handler = new RuntimeEngineFeatures.DeleteRuntimeEngineHandler(repo, NonAdmin());

        var result = await handler.Handle(new RuntimeEngineFeatures.DeleteRuntimeEngineCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("RuntimeEngine.Forbidden", result.Error.Code);
    }
}
