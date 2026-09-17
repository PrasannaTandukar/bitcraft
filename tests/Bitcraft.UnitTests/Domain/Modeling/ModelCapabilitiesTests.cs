using Bitcraft.Api.Mapping;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Xunit;

namespace Bitcraft.UnitTests.Domain.Modeling;

public sealed class ModelCapabilitiesTests
{
    [Fact]
    public void DefaultsToNoneRegardlessOfName()
    {
        var model = new InteractionModel { ProviderModelName = "gpt-4o" };
        Assert.Equal(ModelCapabilities.None, model.Capabilities);
        Assert.False(model.ToResponse().SupportsImageInput);
    }

    [Theory]
    [InlineData(ModelCapabilities.None, false)]
    [InlineData(ModelCapabilities.ImageInput, true)]
    public void SnapshotCapturesCapabilityAndSurvivesLiveChanges(ModelCapabilities capabilities, bool supportsImages)
    {
        var user = Guid.NewGuid();
        var tutor = Tutor.Create("Tutor", null, user);
        var model = new InteractionModel { Capabilities = capabilities };
        var connection = new TutorModelConnection { InteractionModel = model, InteractionModelId = model.Id };
        var revision = new TutorRevision { TutorId = tutor.Id, InteractionModelId = model.Id, InteractionModel = model };
        var legacyCapture = TutorConfigurationSnapshot.Capture(tutor, 1, null, connection, null, null, null, user);
        var activation = TutorConfigurationSnapshot.Capture(tutor, 2, null, null, null, null, null, user, revision);
        Assert.Equal(capabilities, legacyCapture.ModelCapabilities);
        Assert.Equal(capabilities, activation.ModelCapabilities);
        Assert.Equal(supportsImages, model.ToResponse().SupportsImageInput);
        model.Capabilities = supportsImages ? ModelCapabilities.None : ModelCapabilities.ImageInput;
        Assert.Equal(supportsImages, activation.ToResponse().SupportsImageInput);
        Assert.Equal(capabilities, legacyCapture.ModelCapabilities);
        var next = TutorConfigurationSnapshot.Capture(tutor, 3, null, null, null, null, null, user, revision);
        Assert.Equal(!supportsImages, next.ToResponse().SupportsImageInput);
    }
}
