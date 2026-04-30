using Bitcraft.Domain.Documents;
using Bitcraft.Infrastructure.Persistence.Repositories.Documents;
using Xunit;

namespace Bitcraft.InfrastructureIntegrationTests.Persistence;

public class DocumentRepositoryTests
{
    [Fact]
    public async Task ListByCreatorAsync_WithMixedCreators_ReturnsOnlyCreatorDocumentsOrderedByModificationTime()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var currentUser = new MutableCurrentUserContext(ownerId);
        await using var db = PersistenceTestContextFactory.Create(currentUser);
        await PersistenceTestContextFactory.AddUserAsync(db, ownerId);
        await PersistenceTestContextFactory.AddUserAsync(db, otherUserId);
        var repository = new DocumentStore(db);

        var olderOwned = await repository.AddAsync(new Document { Name = "Older owned" });
        currentUser.UserIdOrNull = otherUserId;
        await repository.AddAsync(new Document { Name = "Other user" });
        currentUser.UserIdOrNull = ownerId;
        var newerOwned = await repository.AddAsync(new Document { Name = "Newer owned" });

        olderOwned.Name = "Recently modified owned";
        await repository.UpdateAsync(olderOwned);

        // Act
        var documents = await repository.ListByCreatorAsync(ownerId);

        // Assert
        Assert.Equal(2, documents.Count);
        Assert.All(documents, document => Assert.Equal(ownerId, document.CreatedById));
        Assert.Equal(olderOwned.Id, documents[0].Id);
        Assert.Equal(newerOwned.Id, documents[1].Id);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingDocument_RemovesDocumentFromStore()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUser = new MutableCurrentUserContext(userId);
        await using var db = PersistenceTestContextFactory.Create(currentUser);
        await PersistenceTestContextFactory.AddUserAsync(db, userId);
        var repository = new DocumentStore(db);
        var document = await repository.AddAsync(new Document { Name = "Temporary" });

        // Act
        await repository.DeleteAsync(document);
        var loaded = await repository.GetByIdAsync(document.Id);

        // Assert
        Assert.Null(loaded);
    }
}
