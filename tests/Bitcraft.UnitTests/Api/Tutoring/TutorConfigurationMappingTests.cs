using Bitcraft.Api.Mapping;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Xunit;

namespace Bitcraft.UnitTests.Api.Tutoring;

public class TutorConfigurationMappingTests
{
    [Fact]
    public void TutorResponse_ExposesActiveRevisionAndPromptSummary()
    {
        var prompt = new PromptVersion
        {
            VersionNumber = 3,
            Name = "Robotics tutor",
            Content = "Teach robotics."
        };
        var revision = new TutorRevision
        {
            VersionNumber = 4,
            Status = RevisionStatus.Active,
            PromptVersion = prompt,
            PromptVersionId = prompt.Id
        };
        var tutor = Tutor.Create("Tutor", null, Guid.NewGuid());
        tutor.ActiveRevisionId = revision.Id;
        tutor.ActiveRevision = revision;

        var response = tutor.ToResponse();

        Assert.Equal(revision.Id, response.ActiveRevisionId);
        Assert.Equal(4, response.ActiveRevisionVersionNumber);
        Assert.Equal(RevisionStatus.Active, response.ActiveRevisionStatus);
        Assert.Equal(3, response.ActivePromptVersionNumber);
        Assert.Equal("Robotics tutor", response.ActivePromptVersionName);
    }

    [Fact]
    public void RevisionResponse_ContainsHumanReadableConfigurationAndReadiness()
    {
        var runtime = new RuntimeEngine { Name = "Local Ollama", IsEnabled = true };
        var model = new InteractionModel
        {
            Name = "Qwen 3 30B",
            ProviderModelName = "qwen3:30b",
            RuntimeEngineId = runtime.Id,
            RuntimeEngine = runtime,
            IsEnabled = true
        };
        var embedding = new EmbeddingModel
        {
            Name = "Nomic Embed",
            ProviderModelName = "nomic-embed-text",
            RuntimeEngineId = runtime.Id,
            RuntimeEngine = runtime,
            EmbeddingDimensions = 768,
            IsEnabled = true
        };
        var prompt = new PromptVersion { VersionNumber = 2, Name = "Tutor prompt", Content = "Teach." };
        var revision = new TutorRevision
        {
            VersionNumber = 2,
            Status = RevisionStatus.Draft,
            PromptVersionId = prompt.Id,
            PromptVersion = prompt,
            InteractionModelId = model.Id,
            InteractionModel = model,
            QueryEmbeddingModelId = embedding.Id,
            QueryEmbeddingModel = embedding,
            DeploymentTarget = DeploymentTarget.Server
        };

        var response = revision.ToResponse();

        Assert.Equal(2, response.PromptVersionNumber);
        Assert.Equal("Tutor prompt", response.PromptVersionName);
        Assert.Equal("Qwen 3 30B", response.InteractionModelName);
        Assert.Equal("qwen3:30b", response.ProviderModelName);
        Assert.Equal("Local Ollama", response.RuntimeEngineName);
        Assert.Equal("Nomic Embed", response.QueryEmbeddingModelName);
        Assert.Equal(768, response.QueryEmbeddingDimensions);
        Assert.Equal("Server", response.DeploymentTargetDisplay);
        Assert.True(response.IsPublishable);
        Assert.False(response.IsActive);
    }

    [Fact]
    public void SnapshotResponse_ContainsRevisionRelationshipAndDisplayMetadata()
    {
        var tutor = Tutor.Create("Tutor", null, Guid.NewGuid());
        var prompt = new PromptVersion
        {
            TutorId = tutor.Id,
            VersionNumber = 1,
            Name = "Prompt v1",
            Content = "Teach."
        };
        var model = new InteractionModel { Name = "Llama", ProviderModelName = "llama3.2:1b" };
        var revision = new TutorRevision
        {
            TutorId = tutor.Id,
            VersionNumber = 1,
            PromptVersionId = prompt.Id,
            PromptVersion = prompt,
            InteractionModelId = model.Id,
            InteractionModel = model
        };
        var snapshot = TutorConfigurationSnapshot.Capture(
            tutor,
            1,
            prompt,
            null,
            null,
            null,
            null,
            Guid.NewGuid(),
            tutorRevision: revision);

        var response = snapshot.ToResponse();

        Assert.Equal(revision.Id, response.TutorRevisionId);
        Assert.Equal(1, response.RevisionVersionNumber);
        Assert.Equal("Prompt v1", response.PromptVersionName);
        Assert.Equal("Llama", response.InteractionModelName);
        Assert.Equal(model.Id, response.InteractionModelId);
    }
}
