using Bitcraft.Domain.Modeling;
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

    [Fact]
    public void Create_ProducesDraftStatusWithFoundingOwnerMember()
    {
        // why: D2/D3 require that every Tutor has at least one Owner from inception;
        // the factory is the only creation path that guarantees the founding membership.
        var ownerId = Guid.NewGuid();

        var tutor = Tutor.Create("Algebra", "intro course", ownerId);

        Assert.Equal(TutorStatus.Draft, tutor.Status);
        Assert.Equal("Algebra", tutor.Name);
        Assert.Equal("intro course", tutor.Description);
        var member = Assert.Single(tutor.Members);
        Assert.Equal(ownerId, member.UserId);
        Assert.Equal(ResourceRole.Owner, member.Role);
        Assert.Equal(tutor.Id, member.TutorId);
    }

    [Fact]
    public void Create_FoundingMemberIsSelfGranted()
    {
        // why: at creation time there is no other principal to attribute the grant to;
        // GrantedById must equal the founding user id (self-grant).
        var ownerId = Guid.NewGuid();

        var tutor = Tutor.Create("T", null, ownerId);

        var member = Assert.Single(tutor.Members);
        Assert.Equal(ownerId, member.GrantedById);
        Assert.NotEqual(default, member.GrantedUtc);
    }

    [Fact]
    public void WouldLeaveNoOwner_ReturnsTrue_WhenSoleOwnerTargeted()
    {
        // why: ≥1 Owner invariant — revoking the only Owner must be blocked.
        var ownerId = Guid.NewGuid();
        var tutor = Tutor.Create("T", null, ownerId);
        var soleOwnerMembershipId = tutor.Members.Single().Id;

        Assert.True(tutor.WouldLeaveNoOwner(soleOwnerMembershipId));
    }

    [Fact]
    public void WouldLeaveNoOwner_ReturnsFalse_WhenAnotherOwnerExists()
    {
        // why: a second Owner satisfies the invariant; removing one of them is permissible.
        var firstOwnerId = Guid.NewGuid();
        var secondOwnerId = Guid.NewGuid();
        var tutor = Tutor.Create("T", null, firstOwnerId);
        tutor.Members.Add(new TutorMember
        {
            TutorId = tutor.Id,
            UserId = secondOwnerId,
            Role = ResourceRole.Owner,
            GrantedById = firstOwnerId,
            GrantedUtc = DateTime.UtcNow,
        });

        var firstOwnerMembershipId = tutor.Members.First(m => m.UserId == firstOwnerId).Id;
        Assert.False(tutor.WouldLeaveNoOwner(firstOwnerMembershipId));
    }

    [Fact]
    public void WouldLeaveNoOwner_ReturnsFalse_WhenTargetIsNotOwner()
    {
        // why: removing a non-Owner cannot affect Owner count; invariant is not threatened.
        var ownerId = Guid.NewGuid();
        var editorId = Guid.NewGuid();
        var tutor = Tutor.Create("T", null, ownerId);
        var editor = new TutorMember
        {
            TutorId = tutor.Id,
            UserId = editorId,
            Role = ResourceRole.Editor,
            GrantedById = ownerId,
            GrantedUtc = DateTime.UtcNow,
        };
        tutor.Members.Add(editor);

        Assert.False(tutor.WouldLeaveNoOwner(editor.Id));
    }

    [Fact]
    public void WouldLeaveNoOwner_ReturnsFalse_WhenMembershipNotFound()
    {
        // why: unknown id is a no-op for the invariant check (callers may pass stale ids).
        var tutor = Tutor.Create("T", null, Guid.NewGuid());

        Assert.False(tutor.WouldLeaveNoOwner(Guid.NewGuid()));
    }
}

public class TutorConfigurationSnapshotTests
{
    [Fact]
    public void Capture_WithFullConfiguration_CopiesAllFieldsImmutably()
    {
        // Arrange
        var tutor = Tutor.Create("Algebra", null, Guid.NewGuid());
        var prompt = new PromptVersion { TutorId = tutor.Id, Content = "system prompt", Name = "v1" };
        var interactionModel = new InteractionModel
        {
            Name = "m",
            RuntimeEngineId = Guid.NewGuid(),
            ProviderModelName = "llama3.1",
            SettingsJson = "{\"temp\":0.2}"
        };
        var connection = new TutorModelConnection
        {
            TutorId = tutor.Id,
            InteractionModelId = interactionModel.Id,
            InteractionModel = interactionModel,
            OverrideSettingsJson = "{\"temp\":0.4}"
        };
        var profile = new DeploymentProfile { TutorId = tutor.Id, ConfigurationJson = "{\"replicas\":1}" };
        var capturedById = Guid.NewGuid();

        // Act
        var snapshot = TutorConfigurationSnapshot.Capture(
            tutor, versionNumber: 1, prompt, connection, toolsJson: "[]", knowledgeSourcesJson: "[]", profile, capturedById);

        // Assert
        Assert.NotEqual(Guid.Empty, snapshot.Id);
        Assert.Equal(tutor.Id, snapshot.TutorId);
        Assert.Equal(tutor.Name, snapshot.TutorName);
        Assert.Equal(prompt.Id, snapshot.PromptVersionId);
        Assert.Equal(prompt.Content, snapshot.PromptContent);
        Assert.Equal(connection.Id, snapshot.TutorModelConnectionId);
        Assert.Equal(interactionModel.Id, snapshot.InteractionModelId);
        Assert.Equal(interactionModel.ProviderModelName, snapshot.ModelName);
        Assert.Equal(connection.OverrideSettingsJson, snapshot.ModelSettingsJson);
        Assert.Equal(profile.ConfigurationJson, snapshot.DeploymentProfileJson);
        Assert.Equal(capturedById, snapshot.CapturedById);
    }

    [Fact]
    public void Capture_WithRevision_DoesNotInheritLegacyConnectionOverrides()
    {
        var tutor = Tutor.Create("Algebra", null, Guid.NewGuid());
        var prompt = new PromptVersion { TutorId = tutor.Id, Content = "system prompt", Name = "v1" };
        var interactionModel = new InteractionModel
        {
            Name = "m",
            RuntimeEngineId = Guid.NewGuid(),
            ProviderModelName = "llama3.1",
            SettingsJson = "{\"temperature\":0.2}"
        };
        var legacyConnection = new TutorModelConnection
        {
            TutorId = tutor.Id,
            InteractionModelId = interactionModel.Id,
            InteractionModel = interactionModel,
            OverrideSettingsJson = "{\"temperature\":1.8}"
        };
        var revision = new TutorRevision
        {
            TutorId = tutor.Id,
            VersionNumber = 2,
            PromptVersionId = prompt.Id,
            PromptVersion = prompt,
            InteractionModelId = interactionModel.Id,
            InteractionModel = interactionModel
        };

        var snapshot = TutorConfigurationSnapshot.Capture(
            tutor,
            revision.VersionNumber,
            prompt,
            legacyConnection,
            null,
            null,
            null,
            Guid.NewGuid(),
            revision);

        Assert.Equal(interactionModel.SettingsJson, snapshot.ModelSettingsJson);
        Assert.NotEqual(legacyConnection.OverrideSettingsJson, snapshot.ModelSettingsJson);
        Assert.Null(snapshot.TutorModelConnectionId);
    }

    [Fact]
    public void Capture_WithNullTutor_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            TutorConfigurationSnapshot.Capture(null!, 1, null, null, null, null, null, Guid.NewGuid()));
    }

    [Fact]
    public void Capture_WithEmptyCapturedById_ThrowsArgumentException()
    {
        // Arrange
        var tutor = new Tutor { Name = "T" };

        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            TutorConfigurationSnapshot.Capture(tutor, 1, null, null, null, null, null, Guid.Empty));
    }

    [Fact]
    public void Capture_PropertiesArePrivateSet_RemainImmutableAfterCreation()
    {
        // Arrange
        var tutor = new Tutor { Name = "T" };
        var snapshot = TutorConfigurationSnapshot.Capture(tutor, 1, null, null, null, null, null, Guid.NewGuid());

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
