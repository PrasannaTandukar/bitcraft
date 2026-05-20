using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence.Repositories.Tutoring;
using Xunit;

namespace Bitcraft.InfrastructureIntegrationTests.Persistence;

public class TutorRepositoryTests
{
    [Fact]
    public async Task ListByOwnerAsync_WithMixedOwners_ReturnsOnlyOwnerTutorsOrderedByModificationTime()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        var currentUser = new MutableCurrentUserContext(ownerId);
        await using var db = PersistenceTestContextFactory.Create(currentUser);
        await PersistenceTestContextFactory.AddUserAsync(db, ownerId);
        await PersistenceTestContextFactory.AddUserAsync(db, otherOwnerId);
        var repository = new TutorRepository(db);

        var olderOwned = await repository.AddAsync(Tutor.Create("Older owned", null, ownerId));
        await repository.AddAsync(Tutor.Create("Other owner", null, otherOwnerId));
        var newerOwned = await repository.AddAsync(Tutor.Create("Newer owned", null, ownerId));

        olderOwned.Name = "Recently modified owned";
        await repository.UpdateAsync(olderOwned);

        // Act
        var tutors = await repository.ListByOwnerAsync(ownerId);

        // Assert
        Assert.Equal(2, tutors.Count);
        Assert.All(tutors, tutor => Assert.Equal(ownerId, tutor.Members.FirstOrDefault(m => m.Role == ResourceRole.Owner)?.UserId));
        Assert.Equal(olderOwned.Id, tutors[0].Id);
        Assert.Equal(newerOwned.Id, tutors[1].Id);
    }

    [Fact]
    public async Task UpsertTutorModelConnectionAsync_WithExistingConnection_UpdatesExistingConnection()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUser = new MutableCurrentUserContext(userId);
        await using var db = PersistenceTestContextFactory.Create(currentUser);
        await PersistenceTestContextFactory.AddUserAsync(db, userId);
        var repository = new TutorRepository(db);
        var tutor = await repository.AddAsync(Tutor.Create("Tutor", null, currentUser.UserId));
        var engine = new RuntimeEngine
        {
            Name = "Local Ollama",
            ProviderType = RuntimeEngineProviderType.Ollama,
            Endpoint = "http://localhost:11434",
            IsEnabled = true
        };
        db.RuntimeEngines.Add(engine);
        await db.SaveChangesAsync();
        var firstModel = new InteractionModel
        {
            Name = "First model",
            RuntimeEngineId = engine.Id,
            ProviderModelName = "llama3"
        };
        var secondModel = new InteractionModel
        {
            Name = "Second model",
            RuntimeEngineId = engine.Id,
            ProviderModelName = "mistral"
        };
        db.InteractionModels.AddRange(firstModel, secondModel);
        await db.SaveChangesAsync();
        var original = await repository.UpsertTutorModelConnectionAsync(new TutorModelConnection
        {
            TutorId = tutor.Id,
            InteractionModelId = firstModel.Id,
            OverrideSettingsJson = "{\"temperature\":0.2}"
        });

        // Act
        var updated = await repository.UpsertTutorModelConnectionAsync(new TutorModelConnection
        {
            TutorId = tutor.Id,
            InteractionModelId = secondModel.Id,
            OverrideSettingsJson = "{\"temperature\":0.4}"
        });

        // Assert
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal(secondModel.Id, updated.InteractionModelId);
        Assert.Equal("{\"temperature\":0.4}", updated.OverrideSettingsJson);
    }
}
