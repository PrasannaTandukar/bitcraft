using Bitcraft.Domain.Documents;
using Xunit;

namespace Bitcraft.InfrastructureIntegrationTests.Persistence;

public class AuditableEntitySaveChangesInterceptorTests
{
    [Fact]
    public async Task SaveChangesAsync_WithAddedEntity_PopulatesCreatedAndModifiedMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUser = new MutableCurrentUserContext(userId);
        await using var db = PersistenceTestContextFactory.Create(currentUser);
        await PersistenceTestContextFactory.AddUserAsync(db, userId);
        var document = new Document { Name = "Audit doc" };

        // Act
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        // Assert
        Assert.Equal(userId, document.CreatedById);
        Assert.Equal(userId, document.ModifiedById);
        Assert.NotEqual(default, document.CreatedUtc);
        Assert.NotNull(document.ModifiedUtc);
    }

    [Fact]
    public async Task SaveChangesAsync_WithModifiedEntity_PreservesCreatedMetadataAndUpdatesModifiedMetadata()
    {
        // Arrange
        var creatorId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var currentUser = new MutableCurrentUserContext(creatorId);
        await using var db = PersistenceTestContextFactory.Create(currentUser);
        await PersistenceTestContextFactory.AddUserAsync(db, creatorId);
        await PersistenceTestContextFactory.AddUserAsync(db, modifierId);
        var document = new Document { Name = "Original" };
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        var createdUtc = document.CreatedUtc;
        var firstModifiedUtc = document.ModifiedUtc;

        currentUser.UserIdOrNull = modifierId;

        // Act
        document.Name = "Updated";
        await db.SaveChangesAsync();

        // Assert
        Assert.Equal(creatorId, document.CreatedById);
        Assert.Equal(createdUtc, document.CreatedUtc);
        Assert.Equal(modifierId, document.ModifiedById);
        Assert.True(document.ModifiedUtc >= firstModifiedUtc);
    }

    [Fact]
    public async Task SaveChangesAsync_WithMissingUser_DoesNotOverwriteExistingModifiedMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUser = new MutableCurrentUserContext(userId);
        await using var db = PersistenceTestContextFactory.Create(currentUser);
        await PersistenceTestContextFactory.AddUserAsync(db, userId);
        var document = new Document { Name = "Original" };
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        var modifiedById = document.ModifiedById;
        var modifiedUtc = document.ModifiedUtc;

        currentUser.UserIdOrNull = null;

        // Act
        document.Name = "Updated without user";
        await db.SaveChangesAsync();

        // Assert
        Assert.Equal(modifiedById, document.ModifiedById);
        Assert.Equal(modifiedUtc, document.ModifiedUtc);
    }
}
