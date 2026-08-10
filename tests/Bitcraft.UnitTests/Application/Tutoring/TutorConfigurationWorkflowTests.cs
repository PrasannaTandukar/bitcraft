using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Features.Tutoring;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Bitcraft.UnitTests.Application.Tutoring.Fakes;
using Xunit;

namespace Bitcraft.UnitTests.Application.Tutoring;

public class PromptVersionQueryTests
{
    [Fact]
    public async Task ListPromptVersions_ReturnsOnlyRequestedTutorNewestFirst()
    {
        var owner = Guid.NewGuid();
        var repository = new FakeTutorRepository();
        var tutor = Tutor.Create("Tutor", null, owner);
        var otherTutor = Tutor.Create("Other", null, owner);
        repository.Tutors[tutor.Id] = tutor;
        repository.Tutors[otherTutor.Id] = otherTutor;
        repository.PromptVersions.AddRange([
            new PromptVersion { TutorId = tutor.Id, VersionNumber = 1, Name = "One", Content = "One" },
            new PromptVersion { TutorId = tutor.Id, VersionNumber = 3, Name = "Three", Content = "Three" },
            new PromptVersion { TutorId = otherTutor.Id, VersionNumber = 9, Name = "Other", Content = "Other" }
        ]);
        var handler = new PromptVersionFeatures.ListPromptVersionsHandler(
            repository,
            new FakeTutorMembershipReader(repository),
            new FakeCurrentUserContext(owner));

        var result = await handler.Handle(new PromptVersionFeatures.ListPromptVersionsQuery(tutor.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal([3, 1], result.Value.Select(x => x.VersionNumber));
    }

    [Fact]
    public async Task ListPromptVersions_RequiresTutorMembership()
    {
        var repository = new FakeTutorRepository();
        var tutor = Tutor.Create("Tutor", null, Guid.NewGuid());
        repository.Tutors[tutor.Id] = tutor;
        var handler = new PromptVersionFeatures.ListPromptVersionsHandler(
            repository,
            new FakeTutorMembershipReader(repository),
            new FakeCurrentUserContext(Guid.NewGuid()));

        var result = await handler.Handle(new PromptVersionFeatures.ListPromptVersionsQuery(tutor.Id), default);

        Assert.True(result.IsFailure);
        Assert.Equal("PromptVersion.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task GetPromptVersion_FromDifferentTutorReturnsOpaqueNotFound()
    {
        var owner = Guid.NewGuid();
        var repository = new FakeTutorRepository();
        var tutor = Tutor.Create("Tutor", null, owner);
        var otherTutor = Tutor.Create("Other", null, owner);
        repository.Tutors[tutor.Id] = tutor;
        var prompt = new PromptVersion { TutorId = otherTutor.Id, VersionNumber = 1, Name = "Other", Content = "Other" };
        repository.PromptVersions.Add(prompt);
        var handler = new PromptVersionFeatures.GetPromptVersionHandler(
            repository,
            new FakeTutorMembershipReader(repository),
            new FakeCurrentUserContext(owner));

        var result = await handler.Handle(
            new PromptVersionFeatures.GetPromptVersionQuery(tutor.Id, prompt.Id),
            default);

        Assert.True(result.IsFailure);
        Assert.Equal("PromptVersion.NotFound", result.Error.Code);
    }
}

public class TutorModelCatalogQueryTests
{
    [Fact]
    public async Task Catalog_ReturnsOnlyEnabledModelsOnEnabledRuntimeForTutorMember()
    {
        var owner = Guid.NewGuid();
        var tutors = new FakeTutorRepository();
        var tutor = Tutor.Create("Tutor", null, owner);
        tutors.Tutors[tutor.Id] = tutor;
        var models = new FakeModelRepository();
        var enabledEngine = new RuntimeEngine { Name = "Enabled", IsEnabled = true };
        var disabledEngine = new RuntimeEngine { Name = "Disabled", IsEnabled = false };
        var enabled = new InteractionModel
        {
            Name = "Enabled model", ProviderModelName = "enabled", RuntimeEngine = enabledEngine,
            RuntimeEngineId = enabledEngine.Id, IsEnabled = true
        };
        var disabledModel = new InteractionModel
        {
            Name = "Disabled model", ProviderModelName = "disabled", RuntimeEngine = enabledEngine,
            RuntimeEngineId = enabledEngine.Id, IsEnabled = false
        };
        var disabledRuntime = new InteractionModel
        {
            Name = "Disabled runtime", ProviderModelName = "runtime", RuntimeEngine = disabledEngine,
            RuntimeEngineId = disabledEngine.Id, IsEnabled = true
        };
        models.InteractionModels[enabled.Id] = enabled;
        models.InteractionModels[disabledModel.Id] = disabledModel;
        models.InteractionModels[disabledRuntime.Id] = disabledRuntime;
        var handler = new ModelCatalogFeatures.ListInteractionModelsHandler(
            tutors,
            models,
            new FakeTutorMembershipReader(tutors),
            new FakeCurrentUserContext(owner));

        var result = await handler.Handle(new ModelCatalogFeatures.ListInteractionModelsQuery(tutor.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(enabled.Id, Assert.Single(result.Value).Id);
    }

    [Fact]
    public async Task Catalog_RequiresTutorMembershipAndOnlyAdminMayIncludeDisabled()
    {
        var owner = Guid.NewGuid();
        var tutors = new FakeTutorRepository();
        var tutor = Tutor.Create("Tutor", null, owner);
        tutors.Tutors[tutor.Id] = tutor;
        var models = new FakeModelRepository();
        var runtime = new RuntimeEngine { Name = "Runtime", IsEnabled = true };
        var disabled = new EmbeddingModel
        {
            Name = "Disabled embedding", ProviderModelName = "embed", RuntimeEngine = runtime,
            RuntimeEngineId = runtime.Id, IsEnabled = false, EmbeddingDimensions = 768
        };
        models.EmbeddingModels[disabled.Id] = disabled;

        var unauthorized = await new ModelCatalogFeatures.ListEmbeddingModelsHandler(
            tutors,
            models,
            new FakeTutorMembershipReader(tutors),
            new FakeCurrentUserContext(Guid.NewGuid())).Handle(
            new ModelCatalogFeatures.ListEmbeddingModelsQuery(tutor.Id, IncludeDisabled: true), default);
        Assert.True(unauthorized.IsFailure);
        Assert.Equal("ModelCatalog.Forbidden", unauthorized.Error.Code);

        var admin = await new ModelCatalogFeatures.ListEmbeddingModelsHandler(
            tutors,
            models,
            new FakeTutorMembershipReader(tutors),
            new FakeCurrentUserContext(owner, isAdmin: true)).Handle(
            new ModelCatalogFeatures.ListEmbeddingModelsQuery(tutor.Id, IncludeDisabled: true), default);
        Assert.Equal(disabled.Id, Assert.Single(admin.Value).Id);
    }

    [Fact]
    public async Task EmbeddingCatalog_WithoutTutor_AllowsEditorButRejectsAuditorAndFiltersDisabledRuntimes()
    {
        var models = new FakeModelRepository();
        var enabledRuntime = new RuntimeEngine { Name = "Enabled", IsEnabled = true };
        var disabledRuntime = new RuntimeEngine { Name = "Disabled", IsEnabled = false };
        var enabled = new EmbeddingModel
        {
            Name = "Enabled embedding", ProviderModelName = "enabled", RuntimeEngine = enabledRuntime,
            RuntimeEngineId = enabledRuntime.Id, IsEnabled = true, EmbeddingDimensions = 768
        };
        var unusable = new EmbeddingModel
        {
            Name = "Unavailable embedding", ProviderModelName = "unavailable", RuntimeEngine = disabledRuntime,
            RuntimeEngineId = disabledRuntime.Id, IsEnabled = true, EmbeddingDimensions = 768
        };
        models.EmbeddingModels[enabled.Id] = enabled;
        models.EmbeddingModels[unusable.Id] = unusable;
        var tutors = new FakeTutorRepository();

        var editor = await new ModelCatalogFeatures.ListEmbeddingModelsHandler(
            tutors,
            models,
            new FakeTutorMembershipReader(tutors),
            new FakeCurrentUserContext(Guid.NewGuid(), isEditor: true)).Handle(
            new ModelCatalogFeatures.ListEmbeddingModelsQuery(), default);

        Assert.True(editor.IsSuccess);
        Assert.Equal(enabled.Id, Assert.Single(editor.Value).Id);

        var auditor = await new ModelCatalogFeatures.ListEmbeddingModelsHandler(
            tutors,
            models,
            new FakeTutorMembershipReader(tutors),
            new FakeCurrentUserContext(Guid.NewGuid(), isAuditor: true)).Handle(
            new ModelCatalogFeatures.ListEmbeddingModelsQuery(), default);

        Assert.True(auditor.IsFailure);
        Assert.Equal("ModelCatalog.Forbidden", auditor.Error.Code);
    }
}

public class TutorRevisionWorkflowTests
{
    [Fact]
    public async Task UpdateDraft_RejectsPromptFromAnotherTutor()
    {
        var setup = CreateSetup();
        var foreignPrompt = new PromptVersion
        {
            TutorId = Guid.NewGuid(), VersionNumber = 1, Name = "Foreign", Content = "Foreign"
        };
        setup.Tutors.PromptVersions.Add(foreignPrompt);

        var result = await CreateUpdateHandler(setup).Handle(
            Update(setup, promptVersionId: foreignPrompt.Id), default);

        AssertFailure(result, "PromptVersion.TutorMismatch");
    }

    [Fact]
    public async Task UpdateDraft_RejectsMissingPrompt()
    {
        var setup = CreateSetup();

        var result = await CreateUpdateHandler(setup).Handle(
            Update(setup, promptVersionId: Guid.NewGuid()), default);

        AssertFailure(result, "PromptVersion.NotFound");
    }

    [Fact]
    public async Task UpdateDraft_RejectsMissingInteractionModel()
    {
        var setup = CreateSetup();

        var result = await CreateUpdateHandler(setup).Handle(
            Update(setup, interactionModelId: Guid.NewGuid()), default);

        AssertFailure(result, "InteractionModel.NotFound");
    }

    [Fact]
    public async Task UpdateDraft_RejectsDisabledInteractionModel()
    {
        var setup = CreateSetup();
        var model = AddInteractionModel(setup, enabled: false);

        var result = await CreateUpdateHandler(setup).Handle(
            Update(setup, interactionModelId: model.Id), default);

        AssertFailure(result, "InteractionModel.Disabled");
    }

    [Fact]
    public async Task UpdateDraft_RejectsWrongInteractionModelType()
    {
        var setup = CreateSetup();
        var embedding = AddEmbeddingModel(setup, enabled: true);

        var result = await CreateUpdateHandler(setup).Handle(
            Update(setup, interactionModelId: embedding.Id), default);

        AssertFailure(result, "InteractionModel.InvalidType");
    }

    [Fact]
    public async Task UpdateDraft_RejectsDisabledEmbeddingModel()
    {
        var setup = CreateSetup();
        var embedding = AddEmbeddingModel(setup, enabled: false);

        var result = await CreateUpdateHandler(setup).Handle(
            Update(setup, queryEmbeddingModelId: embedding.Id), default);

        AssertFailure(result, "EmbeddingModel.Disabled");
    }

    [Theory]
    [InlineData("{ broken", null, "TutorRevision.InvalidInteractionParameters")]
    [InlineData(null, "[ broken", "TutorRevision.InvalidDeploymentConfig")]
    public async Task UpdateDraft_RejectsInvalidJson(string? parameters, string? deployment, string expectedCode)
    {
        var setup = CreateSetup();

        var result = await CreateUpdateHandler(setup).Handle(
            Update(setup, interactionParametersJson: parameters, deploymentConfigJson: deployment), default);

        AssertFailure(result, expectedCode);
    }

    [Fact]
    public async Task UpdateDraft_RejectsInvalidDeploymentTarget()
    {
        var setup = CreateSetup();
        var command = Update(setup) with { DeploymentTarget = (DeploymentTarget)999 };

        var result = await CreateUpdateHandler(setup).Handle(command, default);

        AssertFailure(result, "TutorRevision.InvalidDeploymentTarget");
    }

    [Fact]
    public async Task UpdateDraft_RejectsNonDraftAndUnauthorizedMembership()
    {
        var setup = CreateSetup();
        setup.Revision.Status = RevisionStatus.Published;
        var nonDraft = await CreateUpdateHandler(setup).Handle(Update(setup), default);
        AssertFailure(nonDraft, "TutorRevision.NotDraft");

        setup.Revision.Status = RevisionStatus.Draft;
        var unauthorizedHandler = new TutorRevisionFeatures.UpdateDraftRevisionHandler(
            setup.Tutors,
            setup.Models,
            setup.Engines,
            new FakeTutorMembershipReader(),
            new FakeCurrentUserContext(Guid.NewGuid()));
        var unauthorized = await unauthorizedHandler.Handle(Update(setup), default);
        AssertFailure(unauthorized, "TutorRevision.Forbidden");
    }

    [Fact]
    public async Task Publish_RequiresPromptAndInteractionModel()
    {
        var setup = CreateSetup();
        var handler = CreatePublishHandler(setup);

        var noPrompt = await handler.Handle(
            new TutorRevisionFeatures.PublishRevisionCommand(setup.Tutor.Id, setup.Revision.Id), default);
        AssertFailure(noPrompt, "TutorRevision.NotPublishable");

        var prompt = AddPrompt(setup);
        setup.Revision.PromptVersionId = prompt.Id;
        var noModel = await handler.Handle(
            new TutorRevisionFeatures.PublishRevisionCommand(setup.Tutor.Id, setup.Revision.Id), default);
        AssertFailure(noModel, "TutorRevision.NotPublishable");
    }

    [Fact]
    public async Task Publish_ValidDraftBecomesPublishedAndCannotBeEdited()
    {
        var setup = CreateSetup();
        ConfigureValidRevision(setup);

        var published = await CreatePublishHandler(setup).Handle(
            new TutorRevisionFeatures.PublishRevisionCommand(setup.Tutor.Id, setup.Revision.Id), default);
        var edit = await CreateUpdateHandler(setup).Handle(Update(setup), default);

        Assert.True(published.IsSuccess);
        Assert.Equal(RevisionStatus.Published, setup.Revision.Status);
        AssertFailure(edit, "TutorRevision.NotDraft");
    }

    [Fact]
    public async Task Activate_ArchivesPriorRevisionUpdatesTutorAndCreatesLinkedSnapshotAtomically()
    {
        var setup = CreateSetup();
        ConfigureValidRevision(setup);
        setup.Revision.Publish(setup.OwnerId);
        var prior = new TutorRevision
        {
            TutorId = setup.Tutor.Id,
            VersionNumber = 1,
            Status = RevisionStatus.Active,
            ActivationSlug = "old-tutor"
        };
        setup.Revision.VersionNumber = 2;
        setup.Tutor.ActiveRevisionId = prior.Id;
        setup.Tutors.Revisions.Add(prior);
        var effective = new StubEffectiveSetReader([Guid.NewGuid()]);
        var handler = new TutorRevisionFeatures.ActivateRevisionHandler(
            setup.Tutors,
            setup.Models,
            setup.Engines,
            effective,
            setup.Membership,
            setup.CurrentUser);

        var result = await handler.Handle(
            new TutorRevisionFeatures.ActivateRevisionCommand(setup.Tutor.Id, setup.Revision.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(RevisionStatus.Archived, prior.Status);
        Assert.Equal(RevisionStatus.Active, setup.Revision.Status);
        Assert.Equal(setup.Revision.Id, setup.Tutor.ActiveRevisionId);
        Assert.Equal(TutorStatus.Active, setup.Tutor.Status);
        var snapshot = Assert.Single(setup.Tutors.Snapshots);
        Assert.Equal(setup.Revision.Id, snapshot.TutorRevisionId);
        Assert.Equal(setup.Revision.InteractionModelId, snapshot.InteractionModelId);
        Assert.Equal(setup.Revision.PromptVersionId, snapshot.PromptVersionId);
        Assert.Equal(1, setup.Tutors.ActivationSaveCalls);
        Assert.Equal(setup.Revision.QueryEmbeddingModelId, effective.QueryEmbeddingModelId);
    }

    [Fact]
    public async Task Activate_RejectsNonPublishedCrossTutorAndUnauthorizedRequests()
    {
        var setup = CreateSetup();
        ConfigureValidRevision(setup);
        var handler = CreateActivateHandler(setup);

        var draft = await handler.Handle(
            new TutorRevisionFeatures.ActivateRevisionCommand(setup.Tutor.Id, setup.Revision.Id), default);
        AssertFailure(draft, "TutorRevision.NotPublished");

        var crossTutor = await handler.Handle(
            new TutorRevisionFeatures.ActivateRevisionCommand(Guid.NewGuid(), setup.Revision.Id), default);
        AssertFailure(crossTutor, "TutorRevision.NotFound");

        setup.Revision.Publish(setup.OwnerId);
        var unauthorizedHandler = new TutorRevisionFeatures.ActivateRevisionHandler(
            setup.Tutors,
            setup.Models,
            setup.Engines,
            new StubEffectiveSetReader([]),
            new FakeTutorMembershipReader(),
            new FakeCurrentUserContext(Guid.NewGuid()));
        var unauthorized = await unauthorizedHandler.Handle(
            new TutorRevisionFeatures.ActivateRevisionCommand(setup.Tutor.Id, setup.Revision.Id), default);
        AssertFailure(unauthorized, "TutorRevision.Forbidden");
        Assert.Equal(0, setup.Tutors.ActivationSaveCalls);
    }

    [Fact]
    public async Task ArchiveAndDiscard_EnforceLifecycleAndClearActivePointer()
    {
        var setup = CreateSetup();
        setup.Revision.Status = RevisionStatus.Active;
        setup.Revision.ActivationSlug = "active";
        setup.Tutor.ActiveRevisionId = setup.Revision.Id;
        var archive = new TutorRevisionFeatures.ArchiveRevisionHandler(
            setup.Tutors, setup.Membership, setup.CurrentUser);

        var archived = await archive.Handle(
            new TutorRevisionFeatures.ArchiveRevisionCommand(setup.Tutor.Id, setup.Revision.Id), default);

        Assert.True(archived.IsSuccess);
        Assert.Null(setup.Tutor.ActiveRevisionId);
        Assert.Equal(TutorStatus.Inactive, setup.Tutor.Status);

        var discardArchived = await new TutorRevisionFeatures.DiscardDraftRevisionHandler(
            setup.Tutors, setup.Membership, setup.CurrentUser).Handle(
            new TutorRevisionFeatures.DiscardDraftRevisionCommand(setup.Tutor.Id, setup.Revision.Id), default);
        AssertFailure(discardArchived, "TutorRevision.NotDraft");

        var draft = new TutorRevision { TutorId = setup.Tutor.Id, VersionNumber = 2 };
        setup.Tutors.Revisions.Add(draft);
        var discarded = await new TutorRevisionFeatures.DiscardDraftRevisionHandler(
            setup.Tutors, setup.Membership, setup.CurrentUser).Handle(
            new TutorRevisionFeatures.DiscardDraftRevisionCommand(setup.Tutor.Id, draft.Id), default);
        Assert.True(discarded.IsSuccess);
        Assert.DoesNotContain(setup.Tutors.Revisions, x => x.Id == draft.Id);
    }

    private static Setup CreateSetup()
    {
        var owner = Guid.NewGuid();
        var tutors = new FakeTutorRepository();
        var tutor = Tutor.Create("Tutor", null, owner);
        tutors.Tutors[tutor.Id] = tutor;
        var revision = new TutorRevision { TutorId = tutor.Id, VersionNumber = 1 };
        tutors.Revisions.Add(revision);
        return new Setup(
            owner,
            tutor,
            revision,
            tutors,
            new FakeModelRepository(),
            new FakeTutorRuntimeEngineRepository(),
            new FakeTutorMembershipReader(tutors),
            new FakeCurrentUserContext(owner));
    }

    private static PromptVersion AddPrompt(Setup setup)
    {
        var prompt = new PromptVersion
        {
            TutorId = setup.Tutor.Id,
            VersionNumber = 1,
            Name = "Tutor instructions",
            Content = "Answer clearly."
        };
        setup.Tutors.PromptVersions.Add(prompt);
        return prompt;
    }

    private static InteractionModel AddInteractionModel(Setup setup, bool enabled)
    {
        var engine = new RuntimeEngine
        {
            Name = "Local engine",
            Endpoint = "local",
            IsEnabled = true
        };
        setup.Engines.RuntimeEngines[engine.Id] = engine;
        var model = new InteractionModel
        {
            Name = "Qwen",
            ProviderModelName = "qwen3:30b",
            RuntimeEngineId = engine.Id,
            RuntimeEngine = engine,
            IsEnabled = enabled
        };
        setup.Models.InteractionModels[model.Id] = model;
        return model;
    }

    private static EmbeddingModel AddEmbeddingModel(Setup setup, bool enabled)
    {
        var engine = new RuntimeEngine
        {
            Name = "Embedding engine",
            Endpoint = "local",
            IsEnabled = true
        };
        setup.Engines.RuntimeEngines[engine.Id] = engine;
        var model = new EmbeddingModel
        {
            Name = "Nomic Embed",
            ProviderModelName = "nomic-embed-text",
            RuntimeEngineId = engine.Id,
            RuntimeEngine = engine,
            EmbeddingDimensions = 768,
            IsEnabled = enabled
        };
        setup.Models.EmbeddingModels[model.Id] = model;
        return model;
    }

    private static void ConfigureValidRevision(Setup setup)
    {
        var prompt = AddPrompt(setup);
        var interaction = AddInteractionModel(setup, enabled: true);
        var embedding = AddEmbeddingModel(setup, enabled: true);
        setup.Revision.PromptVersionId = prompt.Id;
        setup.Revision.PromptVersion = prompt;
        setup.Revision.InteractionModelId = interaction.Id;
        setup.Revision.InteractionModel = interaction;
        setup.Revision.QueryEmbeddingModelId = embedding.Id;
        setup.Revision.QueryEmbeddingModel = embedding;
        setup.Revision.InteractionParametersJson = "{\"temperature\":0.2}";
        setup.Revision.DeploymentConfigJson = "{}";
    }

    private static TutorRevisionFeatures.UpdateDraftRevisionHandler CreateUpdateHandler(Setup setup) =>
        new(setup.Tutors, setup.Models, setup.Engines, setup.Membership, setup.CurrentUser);

    private static TutorRevisionFeatures.PublishRevisionHandler CreatePublishHandler(Setup setup) =>
        new(setup.Tutors, setup.Models, setup.Engines, setup.Membership, setup.CurrentUser);

    private static TutorRevisionFeatures.ActivateRevisionHandler CreateActivateHandler(Setup setup) =>
        new(
            setup.Tutors,
            setup.Models,
            setup.Engines,
            new StubEffectiveSetReader([]),
            setup.Membership,
            setup.CurrentUser);

    private static TutorRevisionFeatures.UpdateDraftRevisionCommand Update(
        Setup setup,
        Guid? promptVersionId = null,
        Guid? interactionModelId = null,
        Guid? queryEmbeddingModelId = null,
        string? interactionParametersJson = null,
        string? deploymentConfigJson = null) =>
        new(
            setup.Tutor.Id,
            setup.Revision.Id,
            promptVersionId,
            interactionModelId,
            queryEmbeddingModelId,
            interactionParametersJson,
            DeploymentTarget.Local,
            deploymentConfigJson);

    private static void AssertFailure<T>(Bitcraft.Application.Common.Models.Result<T> result, string code)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
    }

    private sealed record Setup(
        Guid OwnerId,
        Tutor Tutor,
        TutorRevision Revision,
        FakeTutorRepository Tutors,
        FakeModelRepository Models,
        FakeTutorRuntimeEngineRepository Engines,
        FakeTutorMembershipReader Membership,
        FakeCurrentUserContext CurrentUser);

    private sealed class StubEffectiveSetReader(IReadOnlyList<Guid> documentIds) : IRevisionEffectiveSetReader
    {
        public Guid? QueryEmbeddingModelId { get; private set; }

        public Task<EffectiveDocumentSet> GetEffectiveSetAsync(
            Guid tutorId,
            CancellationToken cancellationToken = default,
            Guid? queryEmbeddingModelId = null)
        {
            QueryEmbeddingModelId = queryEmbeddingModelId;
            return Task.FromResult(new EffectiveDocumentSet(documentIds, []));
        }
    }
}
