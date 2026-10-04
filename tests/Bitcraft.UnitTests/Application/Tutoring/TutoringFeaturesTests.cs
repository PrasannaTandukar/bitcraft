using Bitcraft.Infrastructure.ExternalServices.Agents;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Features.Admin;
using Bitcraft.Application.Features.Documents;
using Bitcraft.Application.Features.Knowledge;
using Bitcraft.Application.Features.Tutoring;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Bitcraft.UnitTests.Application.Tutoring.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public async Task DeleteTutorHandler_ForOwner_DeletesAndReturnsSuccess()
    {
        // Arrange
        var repo = new FakeTutorRepository();
        var owner = Guid.NewGuid();
        var tutor = Tutor.Create("T", null, owner);
        repo.Tutors[tutor.Id] = tutor;
        var handler = new TutorFeatures.DeleteTutorHandler(repo, new FakeTutorMembershipReader(repo), new FakeCurrentUserContext(owner));
        var command = new TutorFeatures.DeleteTutorCommand(tutor.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(repo.Tutors.ContainsKey(tutor.Id));
    }

    [Fact]
    public async Task DeleteTutorHandler_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var handler = new TutorFeatures.DeleteTutorHandler(new FakeTutorRepository(), new FakeTutorMembershipReader(), new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new TutorFeatures.DeleteTutorCommand(Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Tutor.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task DeleteTutorHandler_ForNonMember_ReturnsForbidden()
    {
        // Arrange
        var repo = new FakeTutorRepository();
        var owner = Guid.NewGuid();
        var tutor = Tutor.Create("T", null, owner);
        repo.Tutors[tutor.Id] = tutor;
        var handler = new TutorFeatures.DeleteTutorHandler(repo, new FakeTutorMembershipReader(repo), new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new TutorFeatures.DeleteTutorCommand(tutor.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Tutor.Forbidden", result.Error.Code);
        Assert.True(repo.Tutors.ContainsKey(tutor.Id));
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
        var handler = new ToolDefinitionFeatures.RegisterToolDefinitionHandler(repo, new FakeTutorMembershipReader(repo), new FakeCurrentUserContext(owner), new TutorToolCatalog([new CalculatorTutorToolHandler()]));
        var command = new ToolDefinitionFeatures.RegisterToolDefinitionCommand(
            tutor.Id, "search", null, "{ not-valid-json");

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
        var handler = new ToolDefinitionFeatures.RegisterToolDefinitionHandler(repo, new FakeTutorMembershipReader(repo), new FakeCurrentUserContext(owner), new TutorToolCatalog([new CalculatorTutorToolHandler()]));
        var command = new ToolDefinitionFeatures.RegisterToolDefinitionCommand(
            tutor.Id, "calculator", "Arithmetic tool",
            "{\"type\":\"object\",\"properties\":{\"expression\":{\"type\":\"string\"}}}",
            TutorToolKind.BuiltIn, "builtin.calculator.v1",
            TutorToolExecutionPolicy.ReadOnlyAutomatic);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("calculator", result.Value.Name);
        Assert.Equal("builtin.calculator.v1", result.Value.ImplementationKey);
        Assert.Single(repo.Tools);
    }

    [Fact]
    public void ToolDefinition_HasNoArbitraryExecutionUrl()
    {
        Assert.Null(typeof(ToolDefinition).GetProperty("ExecutionUrl"));
    }

    [Theory]
    [InlineData(TutorToolKind.Mcp)]
    [InlineData(TutorToolKind.ApprovedHttp)]
    public async Task RegisterToolDefinitionHandler_WithUnavailableRemoteKind_RejectsRegistration(
        TutorToolKind kind)
    {
        var repo = new FakeTutorRepository();
        var owner = Guid.NewGuid();
        var tutor = Tutor.Create("T", null, owner);
        repo.Tutors[tutor.Id] = tutor;
        var handler = new ToolDefinitionFeatures.RegisterToolDefinitionHandler(repo,
            new FakeTutorMembershipReader(repo), new FakeCurrentUserContext(owner), new TutorToolCatalog([new CalculatorTutorToolHandler()]));

        var result = await handler.Handle(new(tutor.Id, "remote", null, "{\"type\":\"object\"}",
            kind, "remote.unavailable.v1", TutorToolExecutionPolicy.ReadOnlyAutomatic), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Tool.KindUnavailable", result.Error.Code);
        Assert.Empty(repo.Tools);
    }
}

public class DocumentFeaturesTests
{
    [Fact]
    public async Task ListDocumentsHandler_BatchesMetadataForOnlyCurrentCreators()
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
        for (var index = 0; index < 14; index++)
        {
            var document = new Document { Name = $"Owned {index}" };
            document.UpdateMetadata(currentUserId);
            documentStore.Documents[document.Id] = document;
        }
        var metadataReader = new CountingDocumentVectorizationReader();
        var handler = new DocumentFeatures.ListDocumentsHandler(documentStore, metadataReader, new FakeCurrentUserContext(currentUserId));

        // Act
        var documents = await handler.Handle(new DocumentFeatures.ListDocumentsQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(15, documents.Count);
        Assert.Contains(documents, item => item.Document.Id == ownedDocument.Id);
        Assert.DoesNotContain(documents, item => item.Document.Id == otherDocument.Id);
        Assert.Equal(1, metadataReader.BulkCalls);
        Assert.Equal(0, metadataReader.SingleCalls);
        Assert.Equal(15, metadataReader.LastRequestedIds!.Count);
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
        var handler = new DocumentFeatures.UpdateDocumentHandler(documentStore, documentStore, new FakeCurrentUserContext(Guid.NewGuid()));
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

    private sealed class CountingDocumentVectorizationReader : IDocumentVectorizationReader
    {
        public int BulkCalls { get; private set; }
        public int SingleCalls { get; private set; }
        public IReadOnlyCollection<Guid>? LastRequestedIds { get; private set; }

        public Task<DocumentVectorizationMetadata> GetMetadataAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            SingleCalls++;
            return Task.FromResult(new DocumentVectorizationMetadata(false, false, 0, null, null, null, null, null, null));
        }

        public Task<IReadOnlyDictionary<Guid, DocumentVectorizationMetadata>> GetMetadataAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken = default)
        {
            BulkCalls++;
            LastRequestedIds = documentIds;
            IReadOnlyDictionary<Guid, DocumentVectorizationMetadata> result = documentIds.ToDictionary(
                id => id,
                _ => new DocumentVectorizationMetadata(false, false, 0, null, null, null, null, null, null));
            return Task.FromResult(result);
        }
    }
}

public class KnowledgeSourceFeaturesTests
{
    [Fact]
    public async Task LegacyLinkRouteWritesCanonicalTutorDocumentWithoutDuplicatingLegacyLinks()
    {
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;
        var documentStore = new FakeDocumentStore();
        var document = new Document { Name = "robotics.pdf" };
        document.UpdateMetadata(ownerId);
        documentStore.Documents[document.Id] = document;
        var handler = new KnowledgeSourceFeatures.LinkKnowledgeSourceHandler(
            tutorRepository,
            documentStore,
            new FakeTutorMembershipReader(tutorRepository),
            new FakeCurrentUserContext(ownerId));

        var result = await handler.Handle(
            new KnowledgeSourceFeatures.LinkKnowledgeSourceCommand(tutor.Id, document.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var canonical = Assert.Single(tutorRepository.TutorDocuments);
        Assert.Equal(document.Id, canonical.DocumentId);
        Assert.Equal(AttachmentSource.Direct, canonical.AttachedVia);
        Assert.Empty(tutorRepository.KnowledgeSources);
    }

    [Fact]
    public async Task AttachDocument_RejectsDocumentAlreadyPresentThroughLegacyCompatibilityLink()
    {
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;
        var documentStore = new FakeDocumentStore();
        var document = new Document { Name = "legacy.pdf" };
        document.UpdateMetadata(ownerId);
        documentStore.Documents[document.Id] = document;
        tutorRepository.KnowledgeSources.Add(new KnowledgeSourceLink
        {
            TutorId = tutor.Id,
            DocumentId = document.Id
        });
        var handler = new KnowledgeSourceFeatures.LinkKnowledgeSourceHandler(
            tutorRepository,
            documentStore,
            new FakeTutorMembershipReader(tutorRepository),
            new FakeCurrentUserContext(ownerId));

        var result = await handler.Handle(
            new KnowledgeSourceFeatures.AttachTutorDocumentCommand(tutor.Id, document.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TutorKnowledge.AlreadyAttached", result.Error.Code);
        Assert.Empty(tutorRepository.TutorDocuments);
    }

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
            new FakeRevisionEffectiveSetReader([firstDocumentId, secondDocumentId]),
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
            new FakeRevisionEffectiveSetReader(),
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
        var document = new Document { Name = "Doc", RawText = new string('a', 200) };
        document.UpdateMetadata(ownerId);
        documentStore.Documents[document.Id] = document;
        var vectorStore = new FakeVectorStore();
        var embeddingProvider = new FakeEmbeddingProvider();
        var handler = new KnowledgeFeatures.VectorizeDocumentHandler(
            documentStore,
            vectorStore,
            embeddingProvider,
            new FakeModelRepository(),
            new FakeCurrentUserContext(ownerId),
            DocumentIngestionLimits.Default,
            NullLogger<KnowledgeFeatures.VectorizeDocumentHandler>.Instance);
        var command = new KnowledgeFeatures.VectorizeDocumentCommand(document.Id, EmbeddingProviders.Ollama, "test-model", MaxChunkLength: 100, ChunkOverlap: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.ChunkCount);
        Assert.Equal(new[] { document.Id }, vectorStore.ReplacedDocumentIds);
        Assert.Equal(2, vectorStore.Chunks.Count);
        Assert.Equal(new[] { new string('a', 100), new string('a', 100) }, embeddingProvider.EmbeddedTexts);
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
            new FakeModelRepository(),
            new FakeCurrentUserContext(ownerId),
            DocumentIngestionLimits.Default,
            NullLogger<KnowledgeFeatures.VectorizeDocumentHandler>.Instance);
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
            new FakeModelRepository(),
            new FakeCurrentUserContext(Guid.NewGuid()),
            DocumentIngestionLimits.Default,
            NullLogger<KnowledgeFeatures.VectorizeDocumentHandler>.Instance);
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
            new FakeModelRepository(),
            new FakeCurrentUserContext(ownerId),
            DocumentIngestionLimits.Default,
            NullLogger<KnowledgeFeatures.VectorizeDocumentHandler>.Instance);
        var command = new KnowledgeFeatures.VectorizeDocumentCommand(document.Id, "UnsupportedProvider", "text-embedding-3-small");

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
            new FakeModelRepository(),
            new FakeCurrentUserContext(ownerId),
            DocumentIngestionLimits.Default,
            NullLogger<KnowledgeFeatures.VectorizeDocumentHandler>.Instance);
        var command = new KnowledgeFeatures.VectorizeDocumentCommand(document.Id, " ollama ", " test-model ", MaxChunkLength: 100, ChunkOverlap: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var chunk = Assert.Single(vectorStore.Chunks);
        Assert.Equal(EmbeddingProviders.Ollama, chunk.EmbeddingProvider);
        Assert.Equal("test-model", chunk.EmbeddingModel);
    }
}
