using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Api.Mapping;
using Bitcraft.Domain.Benchmarking;
using Bitcraft.Domain.Tutoring;
using Xunit;

namespace Bitcraft.UnitTests.Api.Tutoring;

public class BenchmarkMappingTests
{
    [Fact]
    public void ToSummaryResponse_IncludesRevisionAndKeepsExistingSnapshotField()
    {
        var revision = new TutorRevision
        {
            TutorId = Guid.NewGuid(),
            VersionNumber = 7
        };
        var run = new BenchmarkRun
        {
            TutorId = revision.TutorId,
            TutorRevisionId = revision.Id,
            TutorRevision = revision,
            TutorConfigurationSnapshotId = Guid.NewGuid(),
            BenchmarkTemplateId = Guid.NewGuid(),
            GenerateActualOutputs = true,
            UseTutorKnowledge = true,
            RetrievalTopK = 5
        };

        var response = run.ToSummaryResponse();

        Assert.Equal(revision.Id, response.TutorRevisionId);
        Assert.Equal(7, response.TutorRevisionNumber);
        Assert.Equal(run.TutorConfigurationSnapshotId, response.TutorConfigurationSnapshotId);
        Assert.True(response.GenerateActualOutputs);
        Assert.True(response.UseTutorKnowledge);
        Assert.Equal(5, response.RetrievalTopK);
    }

    [Fact]
    public void CreateRequest_DefaultsPreserveExistingOutputEvaluationBehavior()
    {
        var request = new CreateTutorBenchmarkRunRequest(
            Guid.NewGuid(),
            null,
            null,
            null,
            1,
            [new TutorBenchmarkCaseInputRequest("Question", "Existing answer", null, null, null)]);

        Assert.False(request.GenerateActualOutputs);
        Assert.True(request.UseTutorKnowledge);
        Assert.Equal(5, request.TopK);
        Assert.Null(request.TutorRevisionId);
    }
}
