using Microsoft.Extensions.DependencyInjection;
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
        Assert.Contains("AskTutorRuntimeRequest", document.Components!.Schemas!.Keys);
        Assert.Contains("attachmentIds", document.Components.Schemas["AskTutorRuntimeRequest"].Properties!.Keys);
    }
}
