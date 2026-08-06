using System.Text;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Documents;
using Bitcraft.Domain.Documents;
using Bitcraft.UnitTests.Application.Tutoring.Fakes;
using Xunit;

namespace Bitcraft.UnitTests.Application.Documents;

public class DocumentIngestionFeaturesTests
{
    private readonly Guid ownerId = Guid.NewGuid();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../notes.txt")]
    [InlineData("..\\notes.txt")]
    [InlineData("bad\0name.txt")]
    public async Task Upload_RejectsInvalidFileName(string fileName)
    {
        var setup = CreateSetup();
        var result = await setup.Handler.Handle(Command(fileName, "content"u8.ToArray()), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Document.InvalidFileName", result.Error.Code);
        Assert.Empty(setup.Documents.Documents);
        Assert.Equal(0, setup.Extractor.Calls);
    }

    [Fact]
    public async Task Upload_RejectsEmptyPayload()
    {
        var setup = CreateSetup();
        var result = await setup.Handler.Handle(Command("notes.txt", []), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Document.FileEmpty", result.Error.Code);
    }

    [Fact]
    public async Task Upload_RejectsUnsupportedExtension()
    {
        var setup = CreateSetup();
        var result = await setup.Handler.Handle(Command("photo.png", [1, 2, 3]), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Document.UnsupportedFileType", result.Error.Code);
    }

    [Fact]
    public async Task Upload_RejectsOversizedPayload()
    {
        var setup = CreateSetup(new DocumentIngestionLimits(3, 100, 10));
        var result = await setup.Handler.Handle(Command("notes.txt", [1, 2, 3, 4]), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Document.FileTooLarge", result.Error.Code);
    }

    [Fact]
    public async Task Upload_ValidatesFolderOwnership()
    {
        var folder = new DocumentFolder { Name = "Foreign" };
        folder.UpdateMetadata(Guid.NewGuid());
        var setup = CreateSetup(folders: [folder]);
        var result = await setup.Handler.Handle(
            Command("notes.txt", "content"u8.ToArray()) with { FolderId = folder.Id },
            default);

        Assert.True(result.IsFailure);
        Assert.Equal("DocumentFolder.Forbidden", result.Error.Code);
        Assert.Equal(0, setup.Extractor.Calls);
    }

    [Fact]
    public async Task Upload_ExtractsAndStoresRawTextWithSafeMetadata()
    {
        var folder = new DocumentFolder { Name = "Owned" };
        folder.UpdateMetadata(ownerId);
        var setup = CreateSetup(folders: [folder]);
        setup.Extractor.Result = Result.Success(new DocumentTextExtractionResult(
            "Extracted body",
            "text/plain",
            "plain-text"));

        var result = await setup.Handler.Handle(
            Command(" notes.txt ", "source bytes"u8.ToArray()) with { FolderId = folder.Id },
            default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, setup.Extractor.Calls);
        Assert.Equal("notes.txt", result.Value.Name);
        Assert.Equal("text/plain", result.Value.MimeType);
        Assert.Equal("Extracted body", result.Value.RawText);
        Assert.Null(result.Value.SourcePath);
        Assert.Equal(folder.Id, result.Value.FolderId);
    }

    [Fact]
    public async Task Upload_WhenExtractionFailsDoesNotCreateDocument()
    {
        var setup = CreateSetup();
        setup.Extractor.Result = Result.Failure<DocumentTextExtractionResult>(new Error(
            "Document.TextExtractionFailed",
            "Could not extract text."));

        var result = await setup.Handler.Handle(Command("notes.txt", [1]), default);

        Assert.True(result.IsFailure);
        Assert.Empty(setup.Documents.Documents);
    }

    [Theory]
    [InlineData("", "Document.NoExtractableText")]
    [InlineData("   ", "Document.NoExtractableText")]
    [InlineData("four", "Document.ExtractedTextTooLarge")]
    public async Task Upload_RejectsInvalidExtractedText(string extractedText, string errorCode)
    {
        var setup = CreateSetup(new DocumentIngestionLimits(100, 3, 10));
        setup.Extractor.Result = Result.Success(new DocumentTextExtractionResult(
            extractedText,
            "text/plain",
            "plain-text"));

        var result = await setup.Handler.Handle(Command("notes.txt", [1]), default);

        Assert.True(result.IsFailure);
        Assert.Equal(errorCode, result.Error.Code);
        Assert.Empty(setup.Documents.Documents);
    }

    [Fact]
    public async Task Replace_RequiresOwnershipAndPreservesFolderOnSuccess()
    {
        var foreign = new Document { Name = "Foreign", RawText = "old" };
        foreign.UpdateMetadata(Guid.NewGuid());
        var deniedSetup = CreateSetup();
        deniedSetup.Documents.Documents[foreign.Id] = foreign;
        var denied = await CreateReplaceHandler(deniedSetup).Handle(
            ReplaceCommand(foreign.Id, "replacement.txt", [1]), default);
        Assert.Equal("Document.Forbidden", denied.Error.Code);

        var folderId = Guid.NewGuid();
        var owned = new Document { Name = "Old", RawText = "old", FolderId = folderId };
        owned.UpdateMetadata(ownerId);
        var setup = CreateSetup();
        setup.Documents.Documents[owned.Id] = owned;
        setup.Extractor.Result = Result.Success(new DocumentTextExtractionResult("new", "text/plain", "plain-text"));

        var result = await CreateReplaceHandler(setup).Handle(
            ReplaceCommand(owned.Id, "replacement.txt", [1]), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(folderId, result.Value.FolderId);
        Assert.Equal("new", result.Value.RawText);
        Assert.Equal("replacement.txt", result.Value.Name);
        Assert.Equal(1, setup.Documents.ContentReplacementCalls);
    }

    [Fact]
    public async Task Replace_WhenExtractionFailsPreservesExistingContent()
    {
        var owned = new Document { Name = "Old", RawText = "existing text", FolderId = Guid.NewGuid() };
        owned.UpdateMetadata(ownerId);
        var setup = CreateSetup();
        setup.Documents.Documents[owned.Id] = owned;
        setup.Extractor.Result = Result.Failure<DocumentTextExtractionResult>(new Error(
            "Document.TextExtractionFailed",
            "Could not extract text."));

        var result = await CreateReplaceHandler(setup).Handle(
            ReplaceCommand(owned.Id, "replacement.pdf", [1]), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Old", owned.Name);
        Assert.Equal("existing text", owned.RawText);
        Assert.Equal(owned.FolderId, setup.Documents.Documents[owned.Id].FolderId);
        Assert.Equal(0, setup.Documents.ContentReplacementCalls);
    }

    private Setup CreateSetup(
        DocumentIngestionLimits? limits = null,
        IReadOnlyList<DocumentFolder>? folders = null)
    {
        var documents = new FakeDocumentStore();
        var folderStore = new StubFolderStore(folders ?? []);
        var extractor = new StubExtractor();
        var handler = new DocumentFeatures.UploadDocumentHandler(
            documents,
            folderStore,
            extractor,
            limits ?? DocumentIngestionLimits.Default,
            new FakeCurrentUserContext(ownerId));
        return new Setup(handler, documents, extractor, limits ?? DocumentIngestionLimits.Default);
    }

    private DocumentFeatures.ReplaceDocumentContentHandler CreateReplaceHandler(Setup setup) => new(
        setup.Documents,
        setup.Documents,
        setup.Extractor,
        setup.Limits,
        new FakeCurrentUserContext(ownerId));

    private static DocumentFeatures.UploadDocumentCommand Command(string name, byte[] content) =>
        new(name, "application/octet-stream", content.LongLength, content, null);

    private static DocumentFeatures.ReplaceDocumentContentCommand ReplaceCommand(Guid id, string name, byte[] content) =>
        new(id, name, "application/octet-stream", content.LongLength, content);

    private sealed record Setup(
        DocumentFeatures.UploadDocumentHandler Handler,
        FakeDocumentStore Documents,
        StubExtractor Extractor,
        DocumentIngestionLimits Limits);

    private sealed class StubExtractor : IDocumentTextExtractionService
    {
        public int Calls { get; private set; }
        public Result<DocumentTextExtractionResult> Result { get; set; } = Bitcraft.Application.Common.Models.Result.Success(
            new DocumentTextExtractionResult("Extracted", "text/plain", "plain-text"));

        public Task<Result<DocumentTextExtractionResult>> ExtractAsync(
            DocumentTextExtractionRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubFolderStore(IReadOnlyList<DocumentFolder> folders) : IDocumentFolderStore
    {
        public Task<DocumentFolder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(folders.FirstOrDefault(folder => folder.Id == id));

        public Task<DocumentFolder> AddAsync(DocumentFolder folder, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<DocumentFolder>> ListByCreatorAsync(Guid creatorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasChildFoldersAsync(Guid folderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasDocumentsAsync(Guid folderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(DocumentFolder folder, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(DocumentFolder folder, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
