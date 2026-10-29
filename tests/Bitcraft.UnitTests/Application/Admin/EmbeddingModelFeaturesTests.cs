using Bitcraft.Application.Features.Admin;
using Bitcraft.Domain.Modeling;
using Bitcraft.UnitTests.Application.Admin.Fakes;
using Bitcraft.UnitTests.Application.Tutoring.Fakes;
using Xunit;

namespace Bitcraft.UnitTests.Application.Admin;

public class EmbeddingModelFeaturesTests
{
    private static FakeCurrentUserContext Admin() => new(Guid.NewGuid(), isAdmin: true);
    private static FakeCurrentUserContext NonAdmin() => new(Guid.NewGuid(), isAdmin: false);

    private static RuntimeEngine EnabledEngine(FakeRuntimeEngineRepository engines)
    {
        var engine = new RuntimeEngine { Name = "Engine", ProviderType = RuntimeEngineProviderType.Ollama, Endpoint = "http://localhost:11434", IsEnabled = true };
        engines.Engines.Add(engine);
        return engine;
    }

    // ----- Create -----

    [Fact]
    public async Task Create_AsAdmin_PersistsModel()
    {
        var models = new FakeModelRepository();
        var engines = new FakeRuntimeEngineRepository();
        var engine = EnabledEngine(engines);
        var handler = new EmbeddingModelFeatures.CreateEmbeddingModelHandler(models, engines, Admin());
        var cmd = new EmbeddingModelFeatures.CreateEmbeddingModelCommand("Embedder", engine.Id, "text-embedding-3-small", 1536, null, true);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(models.EmbeddingModels);
        Assert.Equal("Embedder", result.Value.Name);
        Assert.Equal(1536, result.Value.EmbeddingDimensions);
    }

    [Fact]
    public async Task Create_AsNonAdmin_ReturnsForbidden()
    {
        var models = new FakeModelRepository();
        var engines = new FakeRuntimeEngineRepository();
        var engine = EnabledEngine(engines);
        var handler = new EmbeddingModelFeatures.CreateEmbeddingModelHandler(models, engines, NonAdmin());
        var cmd = new EmbeddingModelFeatures.CreateEmbeddingModelCommand("Embedder", engine.Id, "text-embedding-3-small", 1536, null, true);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("EmbeddingModel.Forbidden", result.Error.Code);
        Assert.Empty(models.EmbeddingModels);
    }

    [Fact]
    public async Task Create_WithDisabledEngine_ReturnsDisabled()
    {
        var models = new FakeModelRepository();
        var engines = new FakeRuntimeEngineRepository();
        var engine = new RuntimeEngine { Name = "Engine", Endpoint = "http://x", IsEnabled = false };
        engines.Engines.Add(engine);
        var handler = new EmbeddingModelFeatures.CreateEmbeddingModelHandler(models, engines, Admin());
        var cmd = new EmbeddingModelFeatures.CreateEmbeddingModelCommand("Embedder", engine.Id, "text-embedding-3-small", 1536, null, true);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("RuntimeEngine.Disabled", result.Error.Code);
    }

    // ----- List / Get -----

    [Fact]
    public async Task List_AsNonAdmin_ReturnsForbidden()
    {
        var handler = new EmbeddingModelFeatures.ListEmbeddingModelsHandler(new FakeModelRepository(), NonAdmin());
        var result = await handler.Handle(new EmbeddingModelFeatures.ListEmbeddingModelsQuery(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("EmbeddingModel.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Get_WithUnknownId_ReturnsNotFound()
    {
        var handler = new EmbeddingModelFeatures.GetEmbeddingModelHandler(new FakeModelRepository(), Admin());
        var result = await handler.Handle(new EmbeddingModelFeatures.GetEmbeddingModelQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("EmbeddingModel.NotFound", result.Error.Code);
    }

    // ----- Update -----

    [Fact]
    public async Task Update_AsAdmin_AppliesChanges()
    {
        var models = new FakeModelRepository();
        var engines = new FakeRuntimeEngineRepository();
        var engine = EnabledEngine(engines);
        var model = new EmbeddingModel { Name = "Old", RuntimeEngineId = engine.Id, ProviderModelName = "old-model", EmbeddingDimensions = 768, IsEnabled = true };
        models.EmbeddingModels[model.Id] = model;

        var handler = new EmbeddingModelFeatures.UpdateEmbeddingModelHandler(models, engines, Admin());
        var cmd = new EmbeddingModelFeatures.UpdateEmbeddingModelCommand(model.Id, "New", engine.Id, "new-model", 768, null, false);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New", model.Name);
        Assert.Equal("new-model", model.ProviderModelName);
        Assert.Equal(768, model.EmbeddingDimensions);
        Assert.False(model.IsEnabled);
    }

    [Fact]
    public async Task Update_AsNonAdmin_ReturnsForbidden()
    {
        var models = new FakeModelRepository();
        var engines = new FakeRuntimeEngineRepository();
        var engine = EnabledEngine(engines);
        var model = new EmbeddingModel { Name = "Old", RuntimeEngineId = engine.Id, ProviderModelName = "old-model", EmbeddingDimensions = 768, IsEnabled = true };
        models.EmbeddingModels[model.Id] = model;

        var handler = new EmbeddingModelFeatures.UpdateEmbeddingModelHandler(models, engines, NonAdmin());
        var cmd = new EmbeddingModelFeatures.UpdateEmbeddingModelCommand(model.Id, "New", engine.Id, "new-model", 768, null, false);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("EmbeddingModel.Forbidden", result.Error.Code);
        Assert.Equal("Old", model.Name);
    }

    [Fact]
    public async Task Update_WithChangedDimensions_ReturnsImmutableError()
    {
        var models = new FakeModelRepository();
        var engines = new FakeRuntimeEngineRepository();
        var engine = EnabledEngine(engines);
        var model = new EmbeddingModel { Name = "Old", RuntimeEngineId = engine.Id, ProviderModelName = "old-model", EmbeddingDimensions = 768, IsEnabled = true };
        models.EmbeddingModels[model.Id] = model;

        var handler = new EmbeddingModelFeatures.UpdateEmbeddingModelHandler(models, engines, Admin());
        var cmd = new EmbeddingModelFeatures.UpdateEmbeddingModelCommand(model.Id, "Old", engine.Id, "old-model", 1536, null, true);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("EmbeddingModel.DimensionsImmutable", result.Error.Code);
    }

    // ----- Delete -----

    [Fact]
    public async Task Delete_AsAdmin_Removes()
    {
        var models = new FakeModelRepository();
        var model = new EmbeddingModel { Name = "Doomed", ProviderModelName = "m", EmbeddingDimensions = 768, IsEnabled = true };
        models.EmbeddingModels[model.Id] = model;

        var handler = new EmbeddingModelFeatures.DeleteEmbeddingModelHandler(models, Admin());
        var result = await handler.Handle(new EmbeddingModelFeatures.DeleteEmbeddingModelCommand(model.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(models.EmbeddingModels);
    }

    [Fact]
    public async Task Delete_AsNonAdmin_ReturnsForbidden()
    {
        var models = new FakeModelRepository();
        var model = new EmbeddingModel { Name = "Safe", ProviderModelName = "m", EmbeddingDimensions = 768, IsEnabled = true };
        models.EmbeddingModels[model.Id] = model;

        var handler = new EmbeddingModelFeatures.DeleteEmbeddingModelHandler(models, NonAdmin());
        var result = await handler.Handle(new EmbeddingModelFeatures.DeleteEmbeddingModelCommand(model.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("EmbeddingModel.Forbidden", result.Error.Code);
        Assert.Single(models.EmbeddingModels);
    }

    [Fact]
    public async Task Delete_WhenMissing_ReturnsNotFound()
    {
        var handler = new EmbeddingModelFeatures.DeleteEmbeddingModelHandler(new FakeModelRepository(), Admin());
        var result = await handler.Handle(new EmbeddingModelFeatures.DeleteEmbeddingModelCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("EmbeddingModel.NotFound", result.Error.Code);
    }
}
