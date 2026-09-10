using Bitcraft.Application.Features.Admin;
using Bitcraft.Domain.Modeling;
using Bitcraft.UnitTests.Application.Admin.Fakes;
using Bitcraft.UnitTests.Application.Tutoring.Fakes;
using Xunit;

namespace Bitcraft.UnitTests.Application.Admin;

public class InteractionModelFeaturesTests
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
        var handler = new InteractionModelFeatures.CreateInteractionModelHandler(models, engines, Admin());
        var cmd = new InteractionModelFeatures.CreateInteractionModelCommand("GPT", engine.Id, "gpt-4o", null, true);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(models.InteractionModels);
        Assert.Equal("GPT", result.Value.Name);
    }

    [Fact]
    public async Task Create_AsNonAdmin_ReturnsForbidden()
    {
        var models = new FakeModelRepository();
        var engines = new FakeRuntimeEngineRepository();
        var engine = EnabledEngine(engines);
        var handler = new InteractionModelFeatures.CreateInteractionModelHandler(models, engines, NonAdmin());
        var cmd = new InteractionModelFeatures.CreateInteractionModelCommand("GPT", engine.Id, "gpt-4o", null, true);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("InteractionModel.Forbidden", result.Error.Code);
        Assert.Empty(models.InteractionModels);
    }

    [Fact]
    public async Task Create_WithDisabledEngine_ReturnsDisabled()
    {
        var models = new FakeModelRepository();
        var engines = new FakeRuntimeEngineRepository();
        var engine = new RuntimeEngine { Name = "Engine", Endpoint = "http://x", IsEnabled = false };
        engines.Engines.Add(engine);
        var handler = new InteractionModelFeatures.CreateInteractionModelHandler(models, engines, Admin());
        var cmd = new InteractionModelFeatures.CreateInteractionModelCommand("GPT", engine.Id, "gpt-4o", null, true);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("RuntimeEngine.Disabled", result.Error.Code);
    }

    [Fact]
    public async Task CapabilityCanBeCreatedEnabledAndUpdatedWithoutChangingOtherFlags()
    {
        var models = new FakeModelRepository();
        var engines = new FakeRuntimeEngineRepository();
        var engine = EnabledEngine(engines);
        var creator = new InteractionModelFeatures.CreateInteractionModelHandler(models, engines, Admin());
        var created = await creator.Handle(new("Custom", engine.Id, "arbitrary-name", null, true, SupportsImageInput: true), default);
        Assert.True(created.IsSuccess);
        Assert.Equal(ModelCapabilities.ImageInput, created.Value.Capabilities);
        created.Value.Capabilities |= (ModelCapabilities)2;
        var updater = new InteractionModelFeatures.UpdateInteractionModelHandler(models, engines, Admin());
        var updated = await updater.Handle(new(created.Value.Id, "Custom", engine.Id, "arbitrary-name", null, true, SupportsImageInput: false), default);
        Assert.True(updated.IsSuccess);
        Assert.Equal((ModelCapabilities)2, updated.Value.Capabilities);
        updated = await updater.Handle(new(created.Value.Id, "Custom", engine.Id, "arbitrary-name", null, true, SupportsImageInput: true), default);
        Assert.Equal((ModelCapabilities)3, updated.Value.Capabilities);
    }

    // ----- List / Get -----

    [Fact]
    public async Task List_AsNonAdmin_ReturnsForbidden()
    {
        var handler = new InteractionModelFeatures.ListInteractionModelsHandler(new FakeModelRepository(), NonAdmin());
        var result = await handler.Handle(new InteractionModelFeatures.ListInteractionModelsQuery(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("InteractionModel.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Get_WithUnknownId_ReturnsNotFound()
    {
        var handler = new InteractionModelFeatures.GetInteractionModelHandler(new FakeModelRepository(), Admin());
        var result = await handler.Handle(new InteractionModelFeatures.GetInteractionModelQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("InteractionModel.NotFound", result.Error.Code);
    }

    // ----- Update -----

    [Fact]
    public async Task Update_AsAdmin_AppliesChanges()
    {
        var models = new FakeModelRepository();
        var engines = new FakeRuntimeEngineRepository();
        var engine = EnabledEngine(engines);
        var model = new InteractionModel { Name = "Old", RuntimeEngineId = engine.Id, ProviderModelName = "old-model", IsEnabled = true };
        models.InteractionModels[model.Id] = model;

        var handler = new InteractionModelFeatures.UpdateInteractionModelHandler(models, engines, Admin());
        var cmd = new InteractionModelFeatures.UpdateInteractionModelCommand(model.Id, "New", engine.Id, "new-model", null, false);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New", model.Name);
        Assert.Equal("new-model", model.ProviderModelName);
        Assert.False(model.IsEnabled);
    }

    [Fact]
    public async Task Update_AsNonAdmin_ReturnsForbidden()
    {
        var models = new FakeModelRepository();
        var engines = new FakeRuntimeEngineRepository();
        var engine = EnabledEngine(engines);
        var model = new InteractionModel { Name = "Old", RuntimeEngineId = engine.Id, ProviderModelName = "old-model", IsEnabled = true };
        models.InteractionModels[model.Id] = model;

        var handler = new InteractionModelFeatures.UpdateInteractionModelHandler(models, engines, NonAdmin());
        var cmd = new InteractionModelFeatures.UpdateInteractionModelCommand(model.Id, "New", engine.Id, "new-model", null, false);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("InteractionModel.Forbidden", result.Error.Code);
        Assert.Equal("Old", model.Name);
    }

    // ----- Delete -----

    [Fact]
    public async Task Delete_AsAdmin_Removes()
    {
        var models = new FakeModelRepository();
        var model = new InteractionModel { Name = "Doomed", ProviderModelName = "m", IsEnabled = true };
        models.InteractionModels[model.Id] = model;

        var handler = new InteractionModelFeatures.DeleteInteractionModelHandler(models, Admin());
        var result = await handler.Handle(new InteractionModelFeatures.DeleteInteractionModelCommand(model.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(models.InteractionModels);
    }

    [Fact]
    public async Task Delete_AsNonAdmin_ReturnsForbidden()
    {
        var models = new FakeModelRepository();
        var model = new InteractionModel { Name = "Safe", ProviderModelName = "m", IsEnabled = true };
        models.InteractionModels[model.Id] = model;

        var handler = new InteractionModelFeatures.DeleteInteractionModelHandler(models, NonAdmin());
        var result = await handler.Handle(new InteractionModelFeatures.DeleteInteractionModelCommand(model.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("InteractionModel.Forbidden", result.Error.Code);
        Assert.Single(models.InteractionModels);
    }

    [Fact]
    public async Task Delete_WhenMissing_ReturnsNotFound()
    {
        var handler = new InteractionModelFeatures.DeleteInteractionModelHandler(new FakeModelRepository(), Admin());
        var result = await handler.Handle(new InteractionModelFeatures.DeleteInteractionModelCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("InteractionModel.NotFound", result.Error.Code);
    }
}
