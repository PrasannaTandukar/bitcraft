using Bitcraft.Api.Controllers.Tutoring;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace Bitcraft.ApiIntegrationTests;

public sealed class OpenApiDocumentTests(BitcraftApiFactory factory) : IClassFixture<BitcraftApiFactory>
{
    [Fact]
    public void SwaggerGenerationSupportsRuntimeAndMultipartAttachmentContracts()
    {
        using var scope = factory.Services.CreateScope();
        var document = scope.ServiceProvider.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        Assert.Contains("/api/runtime/attachments/images", document.Paths.Keys);
        Assert.Contains("/api/runtime/tutors/{activationSlug}/ask", document.Paths.Keys);
        Assert.Contains("/api/runtime/tutors/{activationSlug}/ask/stream", document.Paths.Keys);
        Assert.Contains("/api/runtime/tutors/{activationSlug}/conversations", document.Paths.Keys);
        Assert.Contains("AskTutorRuntimeRequest", document.Components!.Schemas!.Keys);
        var requestProperties = document.Components.Schemas["AskTutorRuntimeRequest"].Properties!;
        Assert.Contains("attachmentIds", requestProperties.Keys);
        Assert.DoesNotContain("history", requestProperties.Keys);
        Assert.DoesNotContain("/api/ollama/generate", document.Paths.Keys);
        Assert.DoesNotContain("/api/rag/ask", document.Paths.Keys);
        Assert.DoesNotContain("/api/tutor/ask", document.Paths.Keys);
    }

    [Theory]
    [InlineData(nameof(TutorRuntimeController.Ask))]
    [InlineData(nameof(TutorRuntimeController.Stream))]
    public void TutorGenerationEndpointsBoundRequestBodies(string actionName)
    {
        var action = typeof(TutorRuntimeController).GetMethod(actionName,
            BindingFlags.Instance | BindingFlags.Public);
        var limit = Assert.Single(action!.GetCustomAttributes<RequestSizeLimitAttribute>());
        Assert.Equal(256 * 1024, ((IRequestSizeLimitMetadata)limit).MaxRequestBodySize);
    }
}
