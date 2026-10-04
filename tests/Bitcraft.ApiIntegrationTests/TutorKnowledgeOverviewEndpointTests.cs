using System.Net;
using System.Net.Http.Json;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Documents;
using Bitcraft.Domain.Identity;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bitcraft.ApiIntegrationTests;

public sealed partial class TutorRuntimeEndpointTests
{
    [Fact]
    public async Task KnowledgeOverviewListsDirectAndFolderDocumentsFromOneEffectiveSet()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner);
        Guid directId;
        Guid folderDocId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var folder = new DocumentFolder { Name = "Robotics sources" };
            folder.UpdateMetadata(owner);
            var direct = new Document { Name = "direct.md", MimeType = "text/markdown" };
            var inFolder = new Document { Name = "folder.md", MimeType = "text/markdown", FolderId = folder.Id };
            direct.UpdateMetadata(owner);
            inFolder.UpdateMetadata(owner);
            directId = direct.Id;
            folderDocId = inFolder.Id;
            var directLink = new TutorDocument { TutorId = tutor.TutorId, DocumentId = direct.Id, AttachedVia = AttachmentSource.Direct };
            var folderLink = new TutorDocumentFolder { TutorId = tutor.TutorId, DocumentFolderId = folder.Id };
            directLink.UpdateMetadata(owner);
            folderLink.UpdateMetadata(owner);
            db.DocumentFolders.Add(folder);
            db.Documents.AddRange(direct, inFolder);
            db.TutorDocuments.Add(directLink);
            db.TutorDocumentFolders.Add(folderLink);
            await db.SaveChangesAsync();
        }

        var result = await client.GetFromJsonAsync<TutorKnowledgeOverviewResponse>($"/api/tutors/{tutor.TutorId}/knowledge-overview");

        Assert.NotNull(result);
        Assert.Single(result.DirectDocuments);
        Assert.Single(result.Folders);
        Assert.Equal(2, result.EffectiveDocumentCount);
        Assert.Equal(new[] { "direct.md", "folder.md" }, result.EffectiveDocuments.Select(value => value.DocumentName).Order());
        Assert.All(result.EffectiveDocuments, value => Assert.Equal("NotVectorized", value.ReadinessStatus));
        Assert.Contains(result.EffectiveDocuments, value => value.DocumentId == directId && value.AttachedThrough == "Direct");
        Assert.Contains(result.EffectiveDocuments, value => value.DocumentId == folderDocId && value.AttachedThrough == "Folder");
    }

    [Fact]
    public async Task KnowledgeOverviewReturnsSuccessfulZeroAttachmentStateAndRejectsUnreadyCaptureWithoutChangingHistory()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner);
        var empty = await client.GetFromJsonAsync<TutorKnowledgeOverviewResponse>($"/api/tutors/{tutor.TutorId}/knowledge-overview");
        Assert.NotNull(empty);
        Assert.Equal(0, empty.EffectiveDocumentCount);
        Assert.Empty(empty.DirectDocuments);
        Assert.Empty(empty.Folders);
        using var initialCapture = await client.PostAsync($"/api/tutors/{tutor.TutorId}/snapshots/active-revision", null);
        Assert.Equal(HttpStatusCode.Created, initialCapture.StatusCode);

        // Attach an unvectorized document after the original immutable snapshot was captured.
        Guid documentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var document = new Document { Name = "newly-ready.md", MimeType = "text/markdown" };
            document.UpdateMetadata(owner);
            documentId = document.Id;
            db.Documents.Add(document);
            var link = new TutorDocument { TutorId = tutor.TutorId, DocumentId = document.Id };
            link.UpdateMetadata(owner);
            db.TutorDocuments.Add(link);
            await db.SaveChangesAsync();
        }
        using var capture = await client.PostAsync($"/api/tutors/{tutor.TutorId}/snapshots/active-revision", null);
        Assert.Equal(HttpStatusCode.BadRequest, capture.StatusCode);
        using var scopeAfter = factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<AppDbContext>();
        var snapshots = await dbAfter.TutorConfigurationSnapshots.Where(value => value.TutorId == tutor.TutorId).ToListAsync();
        Assert.Equal(2, snapshots.Count);
        var original = snapshots.Single(value => value.Id == tutor.SnapshotId);
        Assert.DoesNotContain(documentId.ToString(), original.EffectiveDocumentIdsJson ?? "");
    }

    [Fact]
    public async Task CapturingReadyKnowledgeCreatesANewSnapshotForTheSameActiveRevision()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner, includeKnowledge: true);
        var documentId = Assert.IsType<Guid>(tutor.DocumentId);
        Guid embeddingModelId;
        int dimensions;
        string embeddingName;
        string providerModel;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var revision = await db.TutorRevisions.Include(value => value.QueryEmbeddingModel)
                .SingleAsync(value => value.Id == tutor.RevisionId);
            embeddingModelId = revision.QueryEmbeddingModelId!.Value;
            dimensions = revision.QueryEmbeddingModel!.EmbeddingDimensions;
            embeddingName = revision.QueryEmbeddingModel.Name;
            providerModel = revision.QueryEmbeddingModel.ProviderModelName;
            var link = new TutorDocument { TutorId = tutor.TutorId, DocumentId = documentId };
            link.UpdateMetadata(owner);
            db.TutorDocuments.Add(link);
            await db.SaveChangesAsync();
        }
        factory.Services.GetRequiredService<TestDocumentVectorizationReader>().Metadata[documentId] =
            new DocumentVectorizationMetadata(true, false, 3, DateTime.UtcNow, embeddingModelId, embeddingName, providerModel, "Ollama", dimensions);

        using var capture = await client.PostAsync($"/api/tutors/{tutor.TutorId}/snapshots/active-revision", null);

        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);
        using var scopeAfter = factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<AppDbContext>();
        var snapshots = await dbAfter.TutorConfigurationSnapshots.Where(value => value.TutorId == tutor.TutorId).ToListAsync();
        Assert.Equal(2, snapshots.Count);
        var original = snapshots.Single(value => value.Id == tutor.SnapshotId);
        var fresh = snapshots.Single(value => value.Id != tutor.SnapshotId);
        Assert.Contains(documentId.ToString(), original.EffectiveDocumentIdsJson ?? "");
        Assert.Contains(documentId.ToString(), fresh.EffectiveDocumentIdsJson ?? "");
        Assert.Equal(tutor.RevisionId, fresh.TutorRevisionId);
    }
}
