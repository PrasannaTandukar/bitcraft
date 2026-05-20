using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Admin;
using Bitcraft.Application.Features.Documents;
using Bitcraft.Application.Features.Knowledge;
using Bitcraft.Application.Features.Tutoring;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Bitcraft.UnitTests.Application.Tutoring.Fakes;
using Xunit;

namespace Bitcraft.UnitTests.Application.Tutoring;

public class TutorFeaturesTests
{
    [Fact]
    public async Task CreateTutorHandler_WithValidInput_ReturnsSuccessAndPersistsDraftTutor()
    {
        // Arrange
        var repo = new FakeTutorRepository();
        var ownerId = Guid.NewGuid();
        var handler = new TutorFeatures.CreateTutorHandler(repo, new FakeCurrentUserContext(ownerId));
        var command = new TutorFeatures.CreateTutorCommand("  Algebra Tutor  ", " Pre-calc helper ");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Algebra Tutor", result.Value.Name);
        Assert.Equal("Pre-calc helper", result.Value.Description);
        Assert.Equal(TutorStatus.Draft, result.Value.Status);
        Assert.Contains(result.Value.Members, m => m.UserId == ownerId && m.Role == ResourceRole.Owner);
        Assert.Single(repo.Tutors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateTutorHandler_WithMissingName_ReturnsNameRequiredFailure(string name)
    {
        // Arrange
        var handler = new TutorFeatures.CreateTutorHandler(new FakeTutorRepository(), new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new TutorFeatures.CreateTutorCommand(name, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Tutor.NameRequired", result.Error.Code);
    }

    [Fact]
    public async Task GetTutorByIdHandler_ForNonOwner_ReturnsForbidden()
    {
        // Arrange
        var repo = new FakeTutorRepository();
        var owner = Guid.NewGuid();
        var tutor = Tutor.Create("T", null, owner);
        repo.Tutors[tutor.Id] = tutor;
        var handler = new TutorFeatures.GetTutorByIdHandler(repo, new FakeTutorMembershipReader(repo), new FakeCurrentUserContext(Guid.NewGuid()));
        var query = new TutorFeatures.GetTutorByIdQuery(tutor.Id);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Tutor.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task GetTutorByIdHandler_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var handler = new TutorFeatures.GetTutorByIdHandler(new FakeTutorRepository(), new FakeTutorMembershipReader(), new FakeCurrentUserContext(Guid.NewGuid()));
        var query = new TutorFeatures.GetTutorByIdQuery(Guid.NewGuid());

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Tutor.NotFound", result.Error.Code);
    }
}

public class TutorModelConnectionFeaturesTests
{
    [Fact]
    public async Task SaveTutorModelConnectionHandler_WithEnabledInteractionModel_PersistsConnection()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;
        var modelRepository = new FakeModelRepository();
        var interactionModel = new InteractionModel
        {
            Name = "Local",
            RuntimeEngineId = Guid.NewGuid(),
            ProviderModelName = "llama3.1",
            IsEnabled = true
        };
        modelRepository.InteractionModels[interactionModel.Id] = interactionModel;
        var handler = new TutorModelConnectionFeatures.SaveTutorModelConnectionHandler(
            tutorRepository,
            modelRepository,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));
        var command = new TutorModelConnectionFeatures.SaveTutorModelConnectionCommand(tutor.Id, interactionModel.Id, "{\"temp\":0.2}");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(interactionModel.Id, result.Value.InteractionModelId);
        Assert.Single(tutorRepository.TutorModelConnections);
    }

    [Fact]
    public async Task SaveTutorModelConnectionHandler_WithDisabledInteractionModel_ReturnsDisabledFailure()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;
        var modelRepository = new FakeModelRepository();
        var interactionModel = new InteractionModel
        {
            Name = "Local",
            RuntimeEngineId = Guid.NewGuid(),
            ProviderModelName = "llama3.1",
            IsEnabled = false
        };
        modelRepository.InteractionModels[interactionModel.Id] = interactionModel;
        var handler = new TutorModelConnectionFeatures.SaveTutorModelConnectionHandler(
            tutorRepository,
            modelRepository,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));
        var command = new TutorModelConnectionFeatures.SaveTutorModelConnectionCommand(tutor.Id, interactionModel.Id, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("InteractionModel.Disabled", result.Error.Code);
    }
}

public class ToolDefinitionFeaturesTests
{
    [Fact]
    public async Task RegisterToolDefinitionHandler_WithMalformedJsonSchema_ReturnsSchemaInvalid()
    {
        // Arrange
        var repo = new FakeTutorRepository();
        var owner = Guid.NewGuid();
        var tutor = Tutor.Create("T", null, owner);
        repo.Tutors[tutor.Id] = tutor;
        var handler = new ToolDefinitionFeatures.RegisterToolDefinitionHandler(repo, new FakeTutorMembershipReader(repo), new FakeCurrentUserContext(owner));
        var command = new ToolDefinitionFeatures.RegisterToolDefinitionCommand(
            tutor.Id, "search", null, "{ not-valid-json", null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Tool.SchemaInvalid", result.Error.Code);
        Assert.Empty(repo.Tools);
    }

    [Fact]
    public async Task RegisterToolDefinitionHandler_WithValidSchemaForOwner_PersistsTool()
    {
        // Arrange
        var repo = new FakeTutorRepository();
        var owner = Guid.NewGuid();
        var tutor = Tutor.Create("T", null, owner);
        repo.Tutors[tutor.Id] = tutor;
        var handler = new ToolDefinitionFeatures.RegisterToolDefinitionHandler(repo, new FakeTutorMembershipReader(repo), new FakeCurrentUserContext(owner));
        var command = new ToolDefinitionFeatures.RegisterToolDefinitionCommand(
            tutor.Id, "search", "Web search tool",
            "{\"type\":\"object\",\"properties\":{\"q\":{\"type\":\"string\"}}}",
            "https://tools.example.com/search");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("search", result.Value.Name);
        Assert.Single(repo.Tools);
    }
}

public class DocumentFeaturesTests
{
    [Fact]
    public async Task ListDocumentsHandler_WithMixedCreators_ReturnsOnlyCurrentCreatorDocuments()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var documentStore = new FakeDocumentStore();
        var ownedDocument = new Document { Name = "Owned" };
        ownedDocument.UpdateMetadata(currentUserId);
        var otherDocument = new Document { Name = "Other" };
        otherDocument.UpdateMetadata(otherUserId);
        documentStore.Documents[ownedDocument.Id] = ownedDocument;
        documentStore.Documents[otherDocument.Id] = otherDocument;
        var handler = new DocumentFeatures.ListDocumentsHandler(documentStore, new FakeCurrentUserContext(currentUserId));

        // Act
        var documents = await handler.Handle(new DocumentFeatures.ListDocumentsQuery(), CancellationToken.None);

        // Assert
        var document = Assert.Single(documents);
        Assert.Equal(ownedDocument.Id, document.Id);
    }

    [Fact]
    public async Task GetDocumentByIdHandler_ForNonOwner_ReturnsForbidden()
    {
        // Arrange
        var documentStore = new FakeDocumentStore();
        var document = new Document { Name = "Doc" };
        document.UpdateMetadata(Guid.NewGuid());
        documentStore.Documents[document.Id] = document;
        var handler = new DocumentFeatures.GetDocumentByIdHandler(documentStore, new FakeCurrentUserContext(Guid.NewGuid()));

        // Act
        var result = await handler.Handle(new DocumentFeatures.GetDocumentByIdQuery(document.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Document.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task UpdateDocumentHandler_ForNonOwner_ReturnsForbidden()
    {
        // Arrange
        var documentStore = new FakeDocumentStore();
        var document = new Document { Name = "Doc" };
        document.UpdateMetadata(Guid.NewGuid());
        documentStore.Documents[document.Id] = document;
        var handler = new DocumentFeatures.UpdateDocumentHandler(documentStore, new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new DocumentFeatures.UpdateDocumentCommand(document.Id, "Updated", null, null, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Document.Forbidden", result.Error.Code);
        Assert.Equal("Doc", document.Name);
    }

    [Fact]
    public async Task DeleteDocumentHandler_ForNonOwner_ReturnsForbidden()
    {
        // Arrange
        var documentStore = new FakeDocumentStore();
        var document = new Document { Name = "Doc" };
        document.UpdateMetadata(Guid.NewGuid());
        documentStore.Documents[document.Id] = document;
        var handler = new DocumentFeatures.DeleteDocumentHandler(documentStore, new FakeCurrentUserContext(Guid.NewGuid()));

        // Act
        var result = await handler.Handle(new DocumentFeatures.DeleteDocumentCommand(document.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Document.Forbidden", result.Error.Code);
        Assert.True(documentStore.Documents.ContainsKey(document.Id));
    }
}

public class KnowledgeSourceFeaturesTests
{
    [Fact]
    public async Task LinkKnowledgeSourceHandler_WithForeignDocument_ReturnsForbidden()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;
        var documentStore = new FakeDocumentStore();
        var document = new Document { Name = "Doc" };
        document.UpdateMetadata(Guid.NewGuid());
        documentStore.Documents[document.Id] = document;
        var handler = new KnowledgeSourceFeatures.LinkKnowledgeSourceHandler(
            tutorRepository,
            documentStore,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));
        var command = new KnowledgeSourceFeatures.LinkKnowledgeSourceCommand(tutor.Id, document.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Document.Forbidden", result.Error.Code);
        Assert.Empty(tutorRepository.KnowledgeSources);
    }
}

public class KnowledgeFeaturesTests
{
    [Fact]
    public async Task SearchKnowledgeHandler_WithTutorKnowledgeSources_SearchesOnlyLinkedDocuments()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Tutor", null, ownerId);
        var firstDocumentId = Guid.NewGuid();
        var secondDocumentId = Guid.NewGuid();
        tutorRepository.Tutors[tutor.Id] = tutor;
        tutorRepository.KnowledgeSources.Add(new KnowledgeSourceLink { TutorId = tutor.Id, DocumentId = firstDocumentId });
        tutorRepository.KnowledgeSources.Add(new KnowledgeSourceLink { TutorId = tutor.Id, DocumentId = secondDocumentId });
        var retriever = new FakeKnowledgeRetriever();
        var handler = new KnowledgeFeatures.SearchKnowledgeHandler(
            retriever,
            tutorRepository,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));
        var queryEmbedding = new[] { 0.1f, 0.2f, 0.3f };
        var query = new KnowledgeFeatures.SearchKnowledgeQuery(tutor.Id, queryEmbedding, TopK: 3);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { firstDocumentId, secondDocumentId }, retriever.LastDocumentIds);
        Assert.Equal(queryEmbedding, retriever.LastQueryEmbedding);
        Assert.Equal(3, retriever.LastTopK);
    }

    [Fact]
    public async Task SearchKnowledgeHandler_ForNonOwner_ReturnsForbidden()
    {
        // Arrange
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Tutor", null, Guid.NewGuid());
        tutorRepository.Tutors[tutor.Id] = tutor;
        var handler = new KnowledgeFeatures.SearchKnowledgeHandler(
            new FakeKnowledgeRetriever(),
            tutorRepository,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(Guid.NewGuid()));
        var query = new KnowledgeFeatures.SearchKnowledgeQuery(tutor.Id, new[] { 0.1f }, TopK: 3);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Tutor.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task VectorizeDocumentHandler_WithRawText_ReplacesKnowledgeChunks()
    {
        // Arrange
        var documentStore = new FakeDocumentStore();
        var ownerId = Guid.NewGuid();
        var document = new Document { Name = "Doc", RawText = "abcdef" };
        document.UpdateMetadata(ownerId);
        documentStore.Documents[document.Id] = document;
        var vectorStore = new FakeVectorStore();
        var embeddingProvider = new FakeEmbeddingProvider();
        var handler = new KnowledgeFeatures.VectorizeDocumentHandler(
            documentStore,
            vectorStore,
            embeddingProvider,
            new FakeCurrentUserContext(ownerId));
        var command = new KnowledgeFeatures.VectorizeDocumentCommand(document.Id, EmbeddingProviders.Ollama, "test-model", MaxChunkLength: 3);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.ChunkCount);
        Assert.Equal(new[] { document.Id }, vectorStore.ReplacedDocumentIds);
        Assert.Equal(2, vectorStore.Chunks.Count);
        Assert.Equal(new[] { "abc", "def" }, embeddingProvider.EmbeddedTexts);
    }

    [Fact]
    public async Task VectorizeDocumentHandler_WithMissingRawText_ReturnsRawTextRequired()
    {
        // Arrange
        var documentStore = new FakeDocumentStore();
        var ownerId = Guid.NewGuid();
        var document = new Document { Name = "Doc", RawText = " " };
        document.UpdateMetadata(ownerId);
        documentStore.Documents[document.Id] = document;
        var handler = new KnowledgeFeatures.VectorizeDocumentHandler(
            documentStore,
            new FakeVectorStore(),
            new FakeEmbeddingProvider(),
            new FakeCurrentUserContext(ownerId));
        var command = new KnowledgeFeatures.VectorizeDocumentCommand(document.Id, EmbeddingProviders.Ollama, "test-model");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Document.RawTextRequired", result.Error.Code);
    }

    [Fact]
    public async Task VectorizeDocumentHandler_ForNonOwner_ReturnsForbidden()
    {
        // Arrange
        var documentStore = new FakeDocumentStore();
        var document = new Document { Name = "Doc", RawText = "abcdef" };
        document.UpdateMetadata(Guid.NewGuid());
        documentStore.Documents[document.Id] = document;
        var vectorStore = new FakeVectorStore();
        var handler = new KnowledgeFeatures.VectorizeDocumentHandler(
            documentStore,
            vectorStore,
            new FakeEmbeddingProvider(),
            new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new KnowledgeFeatures.VectorizeDocumentCommand(document.Id, EmbeddingProviders.Ollama, "test-model");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Document.Forbidden", result.Error.Code);
        Assert.Empty(vectorStore.ReplacedDocumentIds);
    }

    [Fact]
    public async Task VectorizeDocumentHandler_WithUnsupportedEmbeddingProvider_ReturnsUnsupportedProvider()
    {
        // Arrange
        var documentStore = new FakeDocumentStore();
        var ownerId = Guid.NewGuid();
        var document = new Document { Name = "Doc", RawText = "abcdef" };
        document.UpdateMetadata(ownerId);
        documentStore.Documents[document.Id] = document;
        var vectorStore = new FakeVectorStore();
        var handler = new KnowledgeFeatures.VectorizeDocumentHandler(
            documentStore,
            vectorStore,
            new FakeEmbeddingProvider(),
            new FakeCurrentUserContext(ownerId));
        var command = new KnowledgeFeatures.VectorizeDocumentCommand(document.Id, "OpenAI", "text-embedding-3-small");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Knowledge.EmbeddingProviderUnsupported", result.Error.Code);
        Assert.Empty(vectorStore.ReplacedDocumentIds);
    }

    [Fact]
    public async Task VectorizeDocumentHandler_WithProviderCaseAndWhitespace_WritesCanonicalProviderAndTrimmedModel()
    {
        // Arrange
        var documentStore = new FakeDocumentStore();
        var ownerId = Guid.NewGuid();
        var document = new Document { Name = "Doc", RawText = "abcdef" };
        document.UpdateMetadata(ownerId);
        documentStore.Documents[document.Id] = document;
        var vectorStore = new FakeVectorStore();
        var handler = new KnowledgeFeatures.VectorizeDocumentHandler(
            documentStore,
            vectorStore,
            new FakeEmbeddingProvider(),
            new FakeCurrentUserContext(ownerId));
        var command = new KnowledgeFeatures.VectorizeDocumentCommand(document.Id, " ollama ", " test-model ", MaxChunkLength: 6);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var chunk = Assert.Single(vectorStore.Chunks);
        Assert.Equal(EmbeddingProviders.Ollama, chunk.EmbeddingProvider);
        Assert.Equal("test-model", chunk.EmbeddingModel);
    }
}
