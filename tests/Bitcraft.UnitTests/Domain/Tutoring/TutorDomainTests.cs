using Bitcraft.Domain.Tutoring;
using Xunit;

namespace Bitcraft.UnitTests.Domain.Tutoring;

public class TutorTests
{
    [Fact]
    public void Tutor_Initialization_AssignsValidIdAndDraftStatus()
    {
        // Arrange & Act
        var tutor = new Tutor();

        // Assert
        Assert.NotEqual(Guid.Empty, tutor.Id);
        Assert.Equal(TutorStatus.Draft, tutor.Status);
        Assert.NotEqual(default, tutor.CreatedUtc);
    }

    [Fact]
    public void UpdateMetadata_WithUserId_AssignsAuditMetadata()
    {
        // Arrange
        var tutor = new Tutor();
        var userId = Guid.NewGuid();
        // Act
        tutor.UpdateMetadata(userId);

        // Assert
        Assert.Equal(userId, tutor.CreatedById);
        Assert.NotEqual(default, tutor.CreatedUtc);
        Assert.Equal(userId, tutor.ModifiedById);
        Assert.NotNull(tutor.ModifiedUtc);
    }

    [Fact]
    public void UpdateMetadata_WithExistingCreator_PreservesCreatedMetadataAndUpdatesModifiedMetadata()
    {
        // Arrange
        var tutor = new Tutor();
        var creatorId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        tutor.UpdateMetadata(creatorId);
        var createdUtc = tutor.CreatedUtc;
        var firstModifiedUtc = tutor.ModifiedUtc;

        // Act
        tutor.UpdateMetadata(modifierId);

        // Assert
        Assert.Equal(creatorId, tutor.CreatedById);
        Assert.Equal(createdUtc, tutor.CreatedUtc);
        Assert.Equal(modifierId, tutor.ModifiedById);
        Assert.True(tutor.ModifiedUtc >= firstModifiedUtc);
    }
}

public class TutorConfigurationSnapshotTests
{
    [Fact]
    public void Capture_WithFullConfiguration_CopiesAllFieldsImmutably()
    {
        // Arrange
        var tutor = new Tutor { Name = "Algebra", OwnerId = Guid.NewGuid() };
        var prompt = new PromptVersion { TutorId = tutor.Id, Content = "system prompt", Name = "v1" };
        var catalogEntry = new ModelCatalogEntry
        {
            ProviderType = ModelProviderType.Ollama,
            ModelName = "llama3.1",
            Endpoint = "http://localhost:11434",
            SettingsJson = "{\"temp\":0.2}"
        };
        var connection = new TutorModelConnection
        {
            TutorId = tutor.Id,
            ModelCatalogEntryId = catalogEntry.Id,
            ModelCatalogEntry = catalogEntry,
            OverrideSettingsJson = "{\"temp\":0.4}"
        };
        var profile = new DeploymentProfile { TutorId = tutor.Id, ConfigurationJson = "{\"replicas\":1}" };
        var capturedById = Guid.NewGuid();

        // Act
        var snapshot = TutorConfigurationSnapshot.Capture(
            tutor, prompt, connection, toolsJson: "[]", knowledgeSourcesJson: "[]", profile, capturedById);

        // Assert
        Assert.NotEqual(Guid.Empty, snapshot.Id);
        Assert.Equal(tutor.Id, snapshot.TutorId);
        Assert.Equal(tutor.Name, snapshot.TutorName);
        Assert.Equal(prompt.Id, snapshot.PromptVersionId);
        Assert.Equal(prompt.Content, snapshot.PromptContent);
        Assert.Equal(connection.Id, snapshot.TutorModelConnectionId);
        Assert.Equal(catalogEntry.Id, snapshot.ModelCatalogEntryId);
        Assert.Equal(ModelProviderType.Ollama, snapshot.ProviderType);
        Assert.Equal(connection.OverrideSettingsJson, snapshot.ModelSettingsJson);
        Assert.Equal(profile.ConfigurationJson, snapshot.DeploymentProfileJson);
        Assert.Equal(capturedById, snapshot.CapturedById);
    }

    [Fact]
    public void Capture_WithNullTutor_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            TutorConfigurationSnapshot.Capture(null!, null, null, null, null, null, Guid.NewGuid()));
    }

    [Fact]
    public void Capture_WithEmptyCapturedById_ThrowsArgumentException()
    {
        // Arrange
        var tutor = new Tutor { Name = "T" };

        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            TutorConfigurationSnapshot.Capture(tutor, null, null, null, null, null, Guid.Empty));
    }

    [Fact]
    public void Capture_PropertiesArePrivateSet_RemainImmutableAfterCreation()
    {
        // Arrange
        var tutor = new Tutor { Name = "T" };
        var snapshot = TutorConfigurationSnapshot.Capture(tutor, null, null, null, null, null, Guid.NewGuid());

        // Act
        var settable = typeof(TutorConfigurationSnapshot)
            .GetProperties(System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.DeclaredOnly)
            .Where(p => p.GetSetMethod(nonPublic: false) is not null)
            .ToList();

        // Assert
        Assert.Empty(settable);
    }
}
