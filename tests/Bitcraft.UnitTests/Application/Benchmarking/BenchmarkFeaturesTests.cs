using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Benchmarking;
using Bitcraft.Domain.Benchmarking;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Bitcraft.UnitTests.Application.Benchmarking.Fakes;
using Bitcraft.UnitTests.Application.Tutoring.Fakes;
using Xunit;

namespace Bitcraft.UnitTests.Application.Benchmarking;

public class BenchmarkTemplateFeaturesTests
{
    [Fact]
    public async Task ListBenchmarkTemplatesHandler_WhenTemplatesMissing_ReturnsEmptyList()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var repository = new FakeBenchmarkTemplateRepository();
        var handler = new BenchmarkFeatures.ListBenchmarkTemplatesHandler(repository, new FakeCurrentUserContext(ownerId));

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.ListBenchmarkTemplatesQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Empty(repository.Templates);
    }

    [Fact]
    public async Task CreateBenchmarkTemplateHandler_WithInvalidMetricsJson_ReturnsTemplateMetricsInvalid()
    {
        // Arrange
        var handler = new BenchmarkFeatures.CreateBenchmarkTemplateHandler(
            new FakeBenchmarkTemplateRepository(),
            new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new BenchmarkFeatures.CreateBenchmarkTemplateCommand(
            "benchmark-key",
            "Benchmark Name",
            null,
            null,
            "{ not-valid-json",
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.TemplateMetricsInvalid", result.Error.Code);
    }

    [Fact]
    public async Task CreateBenchmarkTemplateHandler_WithMalformedMetricThreshold_ReturnsTemplateMetricsInvalid()
    {
        // Arrange
        var handler = new BenchmarkFeatures.CreateBenchmarkTemplateHandler(
            new FakeBenchmarkTemplateRepository(),
            new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new BenchmarkFeatures.CreateBenchmarkTemplateCommand(
            "benchmark-key",
            "Benchmark Name",
            null,
            null,
            "[{\"key\":\"faithfulness\",\"enabled\":true,\"threshold\":\"bad\"}]",
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.TemplateMetricsInvalid", result.Error.Code);
    }

    [Fact]
    public async Task CreateBenchmarkTemplateHandler_WithUnknownMetricKey_ReturnsTemplateMetricsInvalid()
    {
        // Arrange
        var handler = new BenchmarkFeatures.CreateBenchmarkTemplateHandler(
            new FakeBenchmarkTemplateRepository(),
            new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new BenchmarkFeatures.CreateBenchmarkTemplateCommand(
            "benchmark-key",
            "Benchmark Name",
            null,
            null,
            "[{\"key\":\"unsupported\",\"enabled\":true,\"threshold\":0.85}]",
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.TemplateMetricsInvalid", result.Error.Code);
    }

    [Fact]
    public async Task CreateBenchmarkTemplateHandler_WithThresholdOutsideRange_ReturnsTemplateMetricsInvalid()
    {
        // Arrange
        var handler = new BenchmarkFeatures.CreateBenchmarkTemplateHandler(
            new FakeBenchmarkTemplateRepository(),
            new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new BenchmarkFeatures.CreateBenchmarkTemplateCommand(
            "benchmark-key",
            "Benchmark Name",
            null,
            null,
            "[{\"key\":\"faithfulness\",\"enabled\":true,\"threshold\":1.5}]",
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.TemplateMetricsInvalid", result.Error.Code);
    }

    [Fact]
    public async Task CreateBenchmarkTemplateHandler_WithMetricsObject_PersistsTemplate()
    {
        // Arrange
        var repository = new FakeBenchmarkTemplateRepository();
        var ownerId = Guid.NewGuid();
        var handler = new BenchmarkFeatures.CreateBenchmarkTemplateHandler(
            repository,
            new FakeCurrentUserContext(ownerId));
        var metricsJson = "  {\"metrics\":[{\"key\":\"correctness\",\"enabled\":false,\"threshold\":0.9}]}  ";
        var command = new BenchmarkFeatures.CreateBenchmarkTemplateCommand(
            " benchmark-key ",
            " Benchmark Name ",
            " Description ",
            " docs/benchmarking-templates.md ",
            metricsJson,
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ownerId, result.Value.OwnerId);
        Assert.Equal("benchmark-key", result.Value.Key);
        Assert.Equal("Benchmark Name", result.Value.Name);
        Assert.Equal("Description", result.Value.Description);
        Assert.Equal("docs/benchmarking-templates.md", result.Value.GuidancePath);
        Assert.Equal(metricsJson.Trim(), result.Value.MetricsJson);
        Assert.Contains(result.Value.Id, repository.Templates.Keys);
    }

    [Fact]
    public async Task CreateBenchmarkTemplateHandler_WithDuplicateOwnedKey_ReturnsTemplateKeyAlreadyExists()
    {
        // Arrange
        var repository = new FakeBenchmarkTemplateRepository();
        var ownerId = Guid.NewGuid();
        repository.Templates[Guid.NewGuid()] = new BenchmarkTemplate
        {
            OwnerId = ownerId,
            Key = "benchmark-key",
            Name = "Existing Benchmark",
            MetricsJson = "[]",
            IsEnabled = true
        };
        var handler = new BenchmarkFeatures.CreateBenchmarkTemplateHandler(
            repository,
            new FakeCurrentUserContext(ownerId));
        var command = new BenchmarkFeatures.CreateBenchmarkTemplateCommand(
            " benchmark-key ",
            "Benchmark Name",
            null,
            null,
            "[{\"key\":\"faithfulness\",\"enabled\":true,\"threshold\":0.85}]",
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.TemplateKeyAlreadyExists", result.Error.Code);
    }

    [Fact]
    public async Task CreateBenchmarkTemplateHandler_WithOversizedKey_ReturnsTemplateKeyTooLong()
    {
        // Arrange
        var handler = new BenchmarkFeatures.CreateBenchmarkTemplateHandler(
            new FakeBenchmarkTemplateRepository(),
            new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new BenchmarkFeatures.CreateBenchmarkTemplateCommand(
            new string('k', 121),
            "Benchmark Name",
            null,
            null,
            "[{\"key\":\"faithfulness\",\"enabled\":true,\"threshold\":0.85}]",
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.TemplateKeyTooLong", result.Error.Code);
    }

    [Fact]
    public async Task CreateBenchmarkTemplateHandler_WithOversizedName_ReturnsTemplateNameTooLong()
    {
        // Arrange
        var handler = new BenchmarkFeatures.CreateBenchmarkTemplateHandler(
            new FakeBenchmarkTemplateRepository(),
            new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new BenchmarkFeatures.CreateBenchmarkTemplateCommand(
            "benchmark-key",
            new string('n', 201),
            null,
            null,
            "[{\"key\":\"faithfulness\",\"enabled\":true,\"threshold\":0.85}]",
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.TemplateNameTooLong", result.Error.Code);
    }

    [Fact]
    public async Task CreateBenchmarkTemplateHandler_WithOversizedDescription_ReturnsTemplateDescriptionTooLong()
    {
        // Arrange
        var handler = new BenchmarkFeatures.CreateBenchmarkTemplateHandler(
            new FakeBenchmarkTemplateRepository(),
            new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new BenchmarkFeatures.CreateBenchmarkTemplateCommand(
            "benchmark-key",
            "Benchmark Name",
            new string('d', 2_001),
            null,
            "[{\"key\":\"faithfulness\",\"enabled\":true,\"threshold\":0.85}]",
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.TemplateDescriptionTooLong", result.Error.Code);
    }

    [Fact]
    public async Task CreateBenchmarkTemplateHandler_WithOversizedGuidancePath_ReturnsTemplateGuidancePathTooLong()
    {
        // Arrange
        var handler = new BenchmarkFeatures.CreateBenchmarkTemplateHandler(
            new FakeBenchmarkTemplateRepository(),
            new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new BenchmarkFeatures.CreateBenchmarkTemplateCommand(
            "benchmark-key",
            "Benchmark Name",
            null,
            new string('g', 501),
            "[{\"key\":\"faithfulness\",\"enabled\":true,\"threshold\":0.85}]",
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.TemplateGuidancePathTooLong", result.Error.Code);
    }

    [Fact]
    public async Task CreateBenchmarkTemplateHandler_WithNullMetricsJson_ReturnsTemplateMetricsInvalid()
    {
        // Arrange
        var handler = new BenchmarkFeatures.CreateBenchmarkTemplateHandler(
            new FakeBenchmarkTemplateRepository(),
            new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new BenchmarkFeatures.CreateBenchmarkTemplateCommand(
            "benchmark-key",
            "Benchmark Name",
            null,
            null,
            null,
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.TemplateMetricsInvalid", result.Error.Code);
    }

    [Fact]
    public async Task CreateBenchmarkTemplateHandler_WithNonBooleanEnabledFlag_ReturnsTemplateMetricsInvalid()
    {
        // Arrange
        var handler = new BenchmarkFeatures.CreateBenchmarkTemplateHandler(
            new FakeBenchmarkTemplateRepository(),
            new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new BenchmarkFeatures.CreateBenchmarkTemplateCommand(
            "benchmark-key",
            "Benchmark Name",
            null,
            null,
            "[{\"key\":\"faithfulness\",\"enabled\":\"true\",\"threshold\":0.85}]",
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.TemplateMetricsInvalid", result.Error.Code);
    }

    [Fact]
    public async Task DeleteBenchmarkTemplateHandler_WithTemplateInUse_ReturnsTemplateInUse()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var repository = new FakeBenchmarkTemplateRepository();
        var template = new BenchmarkTemplate
        {
            OwnerId = ownerId,
            Key = "benchmark-key",
            Name = "Benchmark Name",
            MetricsJson = "[]",
            IsEnabled = true
        };
        repository.Templates[template.Id] = template;
        repository.InUseTemplateIds.Add(template.Id);
        var handler = new BenchmarkFeatures.DeleteBenchmarkTemplateHandler(repository, new FakeCurrentUserContext(ownerId));

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.DeleteBenchmarkTemplateCommand(template.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.TemplateInUse", result.Error.Code);
        Assert.Contains(template.Id, repository.Templates.Keys);
    }

    [Fact]
    public async Task DeleteBenchmarkTemplateHandler_WithDifferentOwner_ReturnsTemplateForbidden()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var repository = new FakeBenchmarkTemplateRepository();
        var template = new BenchmarkTemplate
        {
            OwnerId = ownerId,
            Key = "benchmark-key",
            Name = "Benchmark Name",
            MetricsJson = "[]",
            IsEnabled = true
        };
        repository.Templates[template.Id] = template;
        var handler = new BenchmarkFeatures.DeleteBenchmarkTemplateHandler(repository, new FakeCurrentUserContext(Guid.NewGuid()));

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.DeleteBenchmarkTemplateCommand(template.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.TemplateForbidden", result.Error.Code);
        Assert.Contains(template.Id, repository.Templates.Keys);
    }
}

public class BenchmarkRunFeaturesTests
{
    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WithOwnedTutorAndEnabledTemplate_PersistsQueuedRunAndQueuesRun()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;

        var templateRepository = new FakeBenchmarkTemplateRepository();
        var template = new BenchmarkTemplate
        {
            OwnerId = ownerId,
            Key = "benchmark-key",
            Name = "Benchmark Name",
            MetricsJson = "[]",
            IsEnabled = true
        };
        templateRepository.Templates[template.Id] = template;

        var runRepository = new FakeBenchmarkRunRepository();
        var queue = new FakeBenchmarkRunQueue();
        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            tutorRepository,
            templateRepository,
            new FakeModelRepository(),
            runRepository,
            queue,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            tutor.Id,
            template.Id,
            null,
            null,
            "  local-model  ",
            2,
            [new BenchmarkFeatures.BenchmarkCaseInput("  What is 2+2?  ", "  It is 4.  ", "4", "  math  ", "{\"difficulty\":\"easy\"}")]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(BenchmarkRunStatus.Queued, result.Value.Status);
        Assert.Equal(1, result.Value.TotalCaseCount);
        Assert.Equal(0, result.Value.CompletedCaseCount);
        Assert.Equal(0, result.Value.ProgressPercent);
        Assert.Equal("Queued", result.Value.ProgressStage);
        Assert.Equal("local-model", result.Value.ModelAlias);
        var runCase = Assert.Single(result.Value.Cases);
        Assert.Equal("What is 2+2?", runCase.InputText);
        Assert.Equal("It is 4.", runCase.ActualOutput);
        Assert.Equal("math", runCase.ContextText);
        var queuedRunId = Assert.Single(queue.QueuedRunIds);
        Assert.Equal(result.Value.Id, queuedRunId);
        Assert.Contains(result.Value.Id, runRepository.Runs.Keys);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WhenRequestTokenCanceledAfterSave_QueuesRunWithIndependentToken()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;

        var templateRepository = new FakeBenchmarkTemplateRepository();
        var template = new BenchmarkTemplate
        {
            OwnerId = ownerId,
            Key = "benchmark-key",
            Name = "Benchmark Name",
            MetricsJson = "[]",
            IsEnabled = true
        };
        templateRepository.Templates[template.Id] = template;

        var queue = new FakeBenchmarkRunQueue();
        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            tutorRepository,
            templateRepository,
            new FakeModelRepository(),
            new FakeBenchmarkRunRepository(),
            queue,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            tutor.Id,
            template.Id,
            null,
            null,
            null,
            1,
            [new BenchmarkFeatures.BenchmarkCaseInput("Question", "Answer", null, null, null)]);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var result = await handler.Handle(command, cts.Token);

        // Assert
        Assert.True(result.IsSuccess);
        var queuedRunId = Assert.Single(queue.QueuedRunIds);
        Assert.Equal(result.Value.Id, queuedRunId);
        Assert.False(Assert.Single(queue.QueueCancellationTokens).CanBeCanceled);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WithOversizedModelAlias_ReturnsModelAliasTooLongAndDoesNotQueueRun()
    {
        // Arrange
        var runRepository = new FakeBenchmarkRunRepository();
        var queue = new FakeBenchmarkRunQueue();
        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            new FakeTutorRepository(),
            new FakeBenchmarkTemplateRepository(),
            new FakeModelRepository(),
            runRepository,
            queue,
            new FakeTutorMembershipReader(), new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            new string('m', 201),
            1,
            [new BenchmarkFeatures.BenchmarkCaseInput("Question", "Answer", null, null, null)]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.ModelAliasTooLong", result.Error.Code);
        Assert.Empty(runRepository.Runs);
        Assert.Empty(queue.QueuedRunIds);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WithInvalidMetadataJson_ReturnsCaseMetadataInvalid()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;

        var templateRepository = new FakeBenchmarkTemplateRepository();
        var template = new BenchmarkTemplate
        {
            OwnerId = ownerId,
            Key = "benchmark-key",
            Name = "Benchmark Name",
            MetricsJson = "[]",
            IsEnabled = true
        };
        templateRepository.Templates[template.Id] = template;

        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            tutorRepository,
            templateRepository,
            new FakeModelRepository(),
            new FakeBenchmarkRunRepository(),
            new FakeBenchmarkRunQueue(),
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            tutor.Id,
            template.Id,
            null,
            null,
            null,
            1,
            [new BenchmarkFeatures.BenchmarkCaseInput("Question", "Answer", null, null, "{ not-json")]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.CaseMetadataInvalid", result.Error.Code);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_ForNonOwner_ReturnsForbiddenAndDoesNotQueueRun()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;

        var templateRepository = new FakeBenchmarkTemplateRepository();
        var template = new BenchmarkTemplate
        {
            OwnerId = ownerId,
            Key = "benchmark-key",
            Name = "Benchmark Name",
            MetricsJson = "[]",
            IsEnabled = true
        };
        templateRepository.Templates[template.Id] = template;

        var runRepository = new FakeBenchmarkRunRepository();
        var queue = new FakeBenchmarkRunQueue();
        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            tutorRepository,
            templateRepository,
            new FakeModelRepository(),
            runRepository,
            queue,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            tutor.Id,
            template.Id,
            null,
            null,
            null,
            1,
            [new BenchmarkFeatures.BenchmarkCaseInput("Question", "Answer", null, null, null)]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Tutor.Forbidden", result.Error.Code);
        Assert.Empty(runRepository.Runs);
        Assert.Empty(queue.QueuedRunIds);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WithTemplateOwnedByDifferentUser_ReturnsTemplateForbiddenAndDoesNotQueueRun()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;

        var templateRepository = new FakeBenchmarkTemplateRepository();
        var template = new BenchmarkTemplate
        {
            OwnerId = Guid.NewGuid(),
            Key = "benchmark-key",
            Name = "Benchmark Name",
            MetricsJson = "[]",
            IsEnabled = true
        };
        templateRepository.Templates[template.Id] = template;

        var runRepository = new FakeBenchmarkRunRepository();
        var queue = new FakeBenchmarkRunQueue();
        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            tutorRepository,
            templateRepository,
            new FakeModelRepository(),
            runRepository,
            queue,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            tutor.Id,
            template.Id,
            null,
            null,
            null,
            1,
            [new BenchmarkFeatures.BenchmarkCaseInput("Question", "Answer", null, null, null)]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.TemplateForbidden", result.Error.Code);
        Assert.Empty(runRepository.Runs);
        Assert.Empty(queue.QueuedRunIds);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WithDisabledTemplate_ReturnsTemplateDisabledAndDoesNotQueueRun()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;

        var templateRepository = new FakeBenchmarkTemplateRepository();
        var template = new BenchmarkTemplate
        {
            OwnerId = ownerId,
            Key = "benchmark-key",
            Name = "Benchmark Name",
            MetricsJson = "[]",
            IsEnabled = false
        };
        templateRepository.Templates[template.Id] = template;

        var runRepository = new FakeBenchmarkRunRepository();
        var queue = new FakeBenchmarkRunQueue();
        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            tutorRepository,
            templateRepository,
            new FakeModelRepository(),
            runRepository,
            queue,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            tutor.Id,
            template.Id,
            null,
            null,
            null,
            1,
            [new BenchmarkFeatures.BenchmarkCaseInput("Question", "Answer", null, null, null)]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.TemplateDisabled", result.Error.Code);
        Assert.Empty(runRepository.Runs);
        Assert.Empty(queue.QueuedRunIds);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WithWhitespaceActualOutput_ReturnsCaseInvalidAndDoesNotQueueRun()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var runRepository = new FakeBenchmarkRunRepository();
        var queue = new FakeBenchmarkRunQueue();
        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            new FakeTutorRepository(),
            new FakeBenchmarkTemplateRepository(),
            new FakeModelRepository(),
            runRepository,
            queue,
            new FakeTutorMembershipReader(), new FakeCurrentUserContext(ownerId));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            null,
            1,
            [new BenchmarkFeatures.BenchmarkCaseInput("Question", " ", null, null, null)]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.CaseInvalid", result.Error.Code);
        Assert.Empty(runRepository.Runs);
        Assert.Empty(queue.QueuedRunIds);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WithBlankInputText_PersistsPromptlessCase()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;

        var templateRepository = new FakeBenchmarkTemplateRepository();
        var template = new BenchmarkTemplate
        {
            OwnerId = ownerId,
            Key = "benchmark-key",
            Name = "Benchmark Name",
            MetricsJson = "[]",
            IsEnabled = true
        };
        templateRepository.Templates[template.Id] = template;

        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            tutorRepository,
            templateRepository,
            new FakeModelRepository(),
            new FakeBenchmarkRunRepository(),
            new FakeBenchmarkRunQueue(),
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            tutor.Id,
            template.Id,
            null,
            null,
            null,
            1,
            [new BenchmarkFeatures.BenchmarkCaseInput(" ", "The model produced an informative standalone answer.", null, null, null)]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, Assert.Single(result.Value.Cases).InputText);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WithTooManyCases_ReturnsCasesTooManyAndDoesNotQueueRun()
    {
        // Arrange
        var runRepository = new FakeBenchmarkRunRepository();
        var queue = new FakeBenchmarkRunQueue();
        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            new FakeTutorRepository(),
            new FakeBenchmarkTemplateRepository(),
            new FakeModelRepository(),
            runRepository,
            queue,
            new FakeTutorMembershipReader(), new FakeCurrentUserContext(Guid.NewGuid()));
        var cases = Enumerable.Range(0, 201)
            .Select(x => new BenchmarkFeatures.BenchmarkCaseInput($"Question {x}", "Answer", null, null, null))
            .ToList();
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            null,
            1,
            cases);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.CasesTooMany", result.Error.Code);
        Assert.Empty(runRepository.Runs);
        Assert.Empty(queue.QueuedRunIds);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WithInvalidConsistencyRuns_ReturnsConsistencyRunsInvalidAndDoesNotQueueRun()
    {
        // Arrange
        var runRepository = new FakeBenchmarkRunRepository();
        var queue = new FakeBenchmarkRunQueue();
        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            new FakeTutorRepository(),
            new FakeBenchmarkTemplateRepository(),
            new FakeModelRepository(),
            runRepository,
            queue,
            new FakeTutorMembershipReader(), new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            null,
            0,
            [new BenchmarkFeatures.BenchmarkCaseInput("Question", "Answer", null, null, null)]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.ConsistencyRunsInvalid", result.Error.Code);
        Assert.Empty(runRepository.Runs);
        Assert.Empty(queue.QueuedRunIds);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WithOversizedMetadata_ReturnsCaseTooLargeAndDoesNotQueueRun()
    {
        // Arrange
        var runRepository = new FakeBenchmarkRunRepository();
        var queue = new FakeBenchmarkRunQueue();
        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            new FakeTutorRepository(),
            new FakeBenchmarkTemplateRepository(),
            new FakeModelRepository(),
            runRepository,
            queue,
            new FakeTutorMembershipReader(), new FakeCurrentUserContext(Guid.NewGuid()));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            null,
            1,
            [new BenchmarkFeatures.BenchmarkCaseInput("Question", "Answer", null, null, new string('x', 10_001))]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.CaseTooLarge", result.Error.Code);
        Assert.Empty(runRepository.Runs);
        Assert.Empty(queue.QueuedRunIds);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WithSnapshotOutsideTutor_ReturnsSnapshotNotFound()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;

        var templateRepository = new FakeBenchmarkTemplateRepository();
        var template = new BenchmarkTemplate
        {
            OwnerId = ownerId,
            Key = "benchmark-key",
            Name = "Benchmark Name",
            MetricsJson = "[]",
            IsEnabled = true
        };
        templateRepository.Templates[template.Id] = template;

        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            tutorRepository,
            templateRepository,
            new FakeModelRepository(),
            new FakeBenchmarkRunRepository(),
            new FakeBenchmarkRunQueue(),
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            tutor.Id,
            template.Id,
            Guid.NewGuid(),
            null,
            null,
            1,
            [new BenchmarkFeatures.BenchmarkCaseInput("Question", "Answer", null, null, null)]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.SnapshotNotFound", result.Error.Code);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WithMissingInteractionModel_ReturnsInteractionModelNotFound()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;

        var templateRepository = new FakeBenchmarkTemplateRepository();
        var template = new BenchmarkTemplate
        {
            OwnerId = ownerId,
            Key = "benchmark-key",
            Name = "Benchmark Name",
            MetricsJson = "[]",
            IsEnabled = true
        };
        templateRepository.Templates[template.Id] = template;

        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            tutorRepository,
            templateRepository,
            new FakeModelRepository(),
            new FakeBenchmarkRunRepository(),
            new FakeBenchmarkRunQueue(),
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            tutor.Id,
            template.Id,
            null,
            Guid.NewGuid(),
            null,
            1,
            [new BenchmarkFeatures.BenchmarkCaseInput("Question", "Answer", null, null, null)]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("InteractionModel.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WithDisabledInteractionModel_ReturnsInteractionModelDisabled()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;

        var templateRepository = new FakeBenchmarkTemplateRepository();
        var template = new BenchmarkTemplate
        {
            OwnerId = ownerId,
            Key = "benchmark-key",
            Name = "Benchmark Name",
            MetricsJson = "[]",
            IsEnabled = true
        };
        templateRepository.Templates[template.Id] = template;

        var modelCatalogRepository = new FakeModelRepository();
        var interactionModel = new InteractionModel
        {
            Name = "Disabled Model",
            RuntimeEngineId = Guid.NewGuid(),
            ProviderModelName = "llama3",
            IsEnabled = false
        };
        modelCatalogRepository.InteractionModels[interactionModel.Id] = interactionModel;

        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            tutorRepository,
            templateRepository,
            modelCatalogRepository,
            new FakeBenchmarkRunRepository(),
            new FakeBenchmarkRunQueue(),
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            tutor.Id,
            template.Id,
            null,
            interactionModel.Id,
            null,
            1,
            [new BenchmarkFeatures.BenchmarkCaseInput("Question", "Answer", null, null, null)]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("InteractionModel.Disabled", result.Error.Code);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WithSnapshotInteractionModel_PersistsSnapshotInteractionModel()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;

        var templateRepository = new FakeBenchmarkTemplateRepository();
        var template = new BenchmarkTemplate
        {
            OwnerId = ownerId,
            Key = "benchmark-key",
            Name = "Benchmark Name",
            MetricsJson = "[]",
            IsEnabled = true
        };
        templateRepository.Templates[template.Id] = template;

        var modelCatalogRepository = new FakeModelRepository();
        var interactionModel = new InteractionModel
        {
            Name = "Enabled Model",
            RuntimeEngineId = Guid.NewGuid(),
            ProviderModelName = "llama3",
            IsEnabled = true
        };
        modelCatalogRepository.InteractionModels[interactionModel.Id] = interactionModel;
        var tutorModelConnection = new TutorModelConnection
        {
            TutorId = tutor.Id,
            InteractionModelId = interactionModel.Id,
            InteractionModel = interactionModel
        };
        var snapshot = TutorConfigurationSnapshot.Capture(tutor, null, tutorModelConnection, null, null, null, ownerId);
        tutorRepository.Snapshots.Add(snapshot);

        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            tutorRepository,
            templateRepository,
            modelCatalogRepository,
            new FakeBenchmarkRunRepository(),
            new FakeBenchmarkRunQueue(),
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            tutor.Id,
            template.Id,
            snapshot.Id,
            null,
            null,
            1,
            [new BenchmarkFeatures.BenchmarkCaseInput("Question", "Answer", null, null, null)]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(interactionModel.Id, result.Value.InteractionModelId);
    }

    [Fact]
    public async Task CreateTutorBenchmarkRunHandler_WithSnapshotInteractionModelMismatch_ReturnsInteractionModelMismatch()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;

        var templateRepository = new FakeBenchmarkTemplateRepository();
        var template = new BenchmarkTemplate
        {
            OwnerId = ownerId,
            Key = "benchmark-key",
            Name = "Benchmark Name",
            MetricsJson = "[]",
            IsEnabled = true
        };
        templateRepository.Templates[template.Id] = template;

        var snapshotInteractionModel = new InteractionModel
        {
            Name = "Snapshot Model",
            RuntimeEngineId = Guid.NewGuid(),
            ProviderModelName = "llama3",
            IsEnabled = true
        };
        var requestedInteractionModel = new InteractionModel
        {
            Name = "Requested Model",
            RuntimeEngineId = Guid.NewGuid(),
            ProviderModelName = "mistral",
            IsEnabled = true
        };
        var modelCatalogRepository = new FakeModelRepository();
        modelCatalogRepository.InteractionModels[snapshotInteractionModel.Id] = snapshotInteractionModel;
        modelCatalogRepository.InteractionModels[requestedInteractionModel.Id] = requestedInteractionModel;

        var tutorModelConnection = new TutorModelConnection
        {
            TutorId = tutor.Id,
            InteractionModelId = snapshotInteractionModel.Id,
            InteractionModel = snapshotInteractionModel
        };
        var snapshot = TutorConfigurationSnapshot.Capture(tutor, null, tutorModelConnection, null, null, null, ownerId);
        tutorRepository.Snapshots.Add(snapshot);

        var handler = new BenchmarkFeatures.CreateTutorBenchmarkRunHandler(
            tutorRepository,
            templateRepository,
            modelCatalogRepository,
            new FakeBenchmarkRunRepository(),
            new FakeBenchmarkRunQueue(),
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));
        var command = new BenchmarkFeatures.CreateTutorBenchmarkRunCommand(
            tutor.Id,
            template.Id,
            snapshot.Id,
            requestedInteractionModel.Id,
            null,
            1,
            [new BenchmarkFeatures.BenchmarkCaseInput("Question", "Answer", null, null, null)]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.InteractionModelMismatch", result.Error.Code);
    }

    [Fact]
    public async Task ListTutorBenchmarkRunsHandler_ForNonOwner_ReturnsForbidden()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;
        var handler = new BenchmarkFeatures.ListTutorBenchmarkRunsHandler(
            tutorRepository,
            new FakeBenchmarkRunRepository(),
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(Guid.NewGuid()));

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.ListTutorBenchmarkRunsQuery(tutor.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Tutor.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task ListTutorBenchmarkRunsHandler_WithOwnedTutor_ReturnsOnlyTutorRuns()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        var otherTutor = Tutor.Create("Other Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;
        tutorRepository.Tutors[otherTutor.Id] = otherTutor;

        var runRepository = new FakeBenchmarkRunRepository();
        var expectedRun = new BenchmarkRun { TutorId = tutor.Id, BenchmarkTemplateId = Guid.NewGuid() };
        var otherRun = new BenchmarkRun { TutorId = otherTutor.Id, BenchmarkTemplateId = Guid.NewGuid() };
        await runRepository.AddAsync(expectedRun, CancellationToken.None);
        await runRepository.AddAsync(otherRun, CancellationToken.None);
        var handler = new BenchmarkFeatures.ListTutorBenchmarkRunsHandler(
            tutorRepository,
            runRepository,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.ListTutorBenchmarkRunsQuery(tutor.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var run = Assert.Single(result.Value);
        Assert.Equal(expectedRun.Id, run.Id);
    }

    [Fact]
    public async Task GetTutorBenchmarkRunHandler_ForNonOwner_ReturnsForbidden()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;
        var runRepository = new FakeBenchmarkRunRepository();
        await runRepository.AddAsync(new BenchmarkRun { TutorId = tutor.Id, BenchmarkTemplateId = Guid.NewGuid() }, CancellationToken.None);
        var handler = new BenchmarkFeatures.GetTutorBenchmarkRunHandler(
            tutorRepository,
            runRepository,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(Guid.NewGuid()));

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.GetTutorBenchmarkRunQuery(tutor.Id, Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Tutor.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task GetTutorBenchmarkRunHandler_WithRunFromDifferentTutor_ReturnsRunNotFound()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        var otherTutor = Tutor.Create("Other Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;
        tutorRepository.Tutors[otherTutor.Id] = otherTutor;

        var run = new BenchmarkRun { TutorId = otherTutor.Id, BenchmarkTemplateId = Guid.NewGuid() };
        var runRepository = new FakeBenchmarkRunRepository();
        await runRepository.AddAsync(run, CancellationToken.None);
        var handler = new BenchmarkFeatures.GetTutorBenchmarkRunHandler(
            tutorRepository,
            runRepository,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.GetTutorBenchmarkRunQuery(tutor.Id, run.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.RunNotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetTutorBenchmarkRunProgressHandler_WithOwnedTutor_ReturnsRunProgress()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;

        var run = new BenchmarkRun
        {
            TutorId = tutor.Id,
            BenchmarkTemplateId = Guid.NewGuid(),
            Status = BenchmarkRunStatus.Running,
            TotalCaseCount = 4,
            CompletedCaseCount = 1,
            ProgressPercent = 25,
            ProgressStage = "Running",
            ProgressMessage = "Benchmark evaluation is running."
        };
        var runRepository = new FakeBenchmarkRunRepository();
        await runRepository.AddAsync(run, CancellationToken.None);
        var handler = new BenchmarkFeatures.GetTutorBenchmarkRunProgressHandler(
            tutorRepository,
            runRepository,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.GetTutorBenchmarkRunProgressQuery(tutor.Id, run.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(BenchmarkRunStatus.Running, result.Value.Status);
        Assert.Equal(4, result.Value.TotalCaseCount);
        Assert.Equal(1, result.Value.CompletedCaseCount);
        Assert.Equal(25, result.Value.ProgressPercent);
        Assert.Equal("Running", result.Value.ProgressStage);
    }

    [Fact]
    public async Task GetTutorBenchmarkRunProgressHandler_ForNonOwner_ReturnsForbidden()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;
        var runRepository = new FakeBenchmarkRunRepository();
        var run = new BenchmarkRun { TutorId = tutor.Id, BenchmarkTemplateId = Guid.NewGuid() };
        await runRepository.AddAsync(run, CancellationToken.None);
        var handler = new BenchmarkFeatures.GetTutorBenchmarkRunProgressHandler(
            tutorRepository,
            runRepository,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(Guid.NewGuid()));

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.GetTutorBenchmarkRunProgressQuery(tutor.Id, run.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Tutor.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task GetTutorBenchmarkRunProgressHandler_WithRunFromDifferentTutor_ReturnsRunNotFound()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var tutorRepository = new FakeTutorRepository();
        var tutor = Tutor.Create("Benchmark Tutor", null, ownerId);
        var otherTutor = Tutor.Create("Other Tutor", null, ownerId);
        tutorRepository.Tutors[tutor.Id] = tutor;
        tutorRepository.Tutors[otherTutor.Id] = otherTutor;

        var run = new BenchmarkRun { TutorId = otherTutor.Id, BenchmarkTemplateId = Guid.NewGuid() };
        var runRepository = new FakeBenchmarkRunRepository();
        await runRepository.AddAsync(run, CancellationToken.None);
        var handler = new BenchmarkFeatures.GetTutorBenchmarkRunProgressHandler(
            tutorRepository,
            runRepository,
            new FakeTutorMembershipReader(tutorRepository), new FakeCurrentUserContext(ownerId));

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.GetTutorBenchmarkRunProgressQuery(tutor.Id, run.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.RunNotFound", result.Error.Code);
    }

    [Fact]
    public async Task ProcessQueuedBenchmarkRunHandler_WithMissingRun_ReturnsRunNotFound()
    {
        // Arrange
        var repository = new FakeBenchmarkRunRepository();
        var engine = new FakeBenchmarkEngine();
        var handler = new BenchmarkFeatures.ProcessQueuedBenchmarkRunHandler(repository, engine);

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.ProcessQueuedBenchmarkRunCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.RunNotFound", result.Error.Code);
        Assert.Null(engine.LastRequest);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task ProcessQueuedBenchmarkRunHandler_WithCompletedRun_ReturnsSuccessWithoutReprocessing()
    {
        // Arrange
        var run = new BenchmarkRun
        {
            TutorId = Guid.NewGuid(),
            BenchmarkTemplateId = Guid.NewGuid(),
            Status = BenchmarkRunStatus.Completed,
            CompletedUtc = DateTime.UtcNow,
            Cases =
            [
                new BenchmarkRunCase
                {
                    CaseIndex = 0,
                    InputText = "Question",
                    ActualOutput = "Answer"
                }
            ]
        };
        var repository = new FakeBenchmarkRunRepository();
        await repository.AddAsync(run, CancellationToken.None);
        var engine = new FakeBenchmarkEngine();
        var handler = new BenchmarkFeatures.ProcessQueuedBenchmarkRunHandler(repository, engine);

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.ProcessQueuedBenchmarkRunCommand(run.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(BenchmarkRunStatus.Completed, run.Status);
        Assert.Null(engine.LastRequest);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task ProcessQueuedBenchmarkRunHandler_WithEngineFailure_MarksRunFailed()
    {
        // Arrange
        var template = new BenchmarkTemplate
        {
            OwnerId = Guid.NewGuid(),
            Key = "benchmark-key",
            Name = "Benchmark Name",
            GuidancePath = "docs/benchmarking-templates.md",
            MetricsJson = "[]",
            IsEnabled = true
        };
        var run = new BenchmarkRun
        {
            TutorId = Guid.NewGuid(),
            BenchmarkTemplateId = template.Id,
            BenchmarkTemplate = template,
            ConsistencyRuns = 1,
            Status = BenchmarkRunStatus.Queued,
            Cases =
            [
                new BenchmarkRunCase
                {
                    CaseIndex = 0,
                    InputText = "Question",
                    ActualOutput = "Answer",
                    ExpectedOutput = "Answer"
                }
            ]
        };

        var repository = new FakeBenchmarkRunRepository();
        await repository.AddAsync(run, CancellationToken.None);
        var engine = new FakeBenchmarkEngine
        {
            ResultToReturn = Result.Failure<BenchmarkEngineResult>(new Error("Benchmark.EngineFailed", "DeepEval request failed."))
        };
        var handler = new BenchmarkFeatures.ProcessQueuedBenchmarkRunHandler(repository, engine);

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.ProcessQueuedBenchmarkRunCommand(run.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.EngineFailed", result.Error.Code);
        Assert.Equal(BenchmarkRunStatus.Failed, run.Status);
        Assert.Equal("Failed", run.ProgressStage);
        Assert.Equal("DeepEval request failed.", run.ProgressMessage);
        Assert.NotNull(run.StartedUtc);
        Assert.NotNull(run.CompletedUtc);
        Assert.Equal("Benchmark.EngineFailed", run.FailureCode);
        Assert.Equal(2, repository.UpdateCalls);
    }

    [Fact]
    public async Task ProcessQueuedBenchmarkRunHandler_WithSidecarException_StoresSanitizedFailure()
    {
        // Arrange
        var template = new BenchmarkTemplate
        {
            OwnerId = Guid.NewGuid(),
            Key = "benchmark-key",
            Name = "Benchmark Name",
            GuidancePath = "docs/benchmarking-templates.md",
            MetricsJson = "[]",
            IsEnabled = true
        };
        var run = new BenchmarkRun
        {
            TutorId = Guid.NewGuid(),
            BenchmarkTemplateId = template.Id,
            BenchmarkTemplate = template,
            ConsistencyRuns = 1,
            Status = BenchmarkRunStatus.Queued,
            Cases =
            [
                new BenchmarkRunCase
                {
                    CaseIndex = 0,
                    InputText = "Question",
                    ActualOutput = "Answer",
                    ExpectedOutput = "Answer"
                }
            ]
        };

        var repository = new FakeBenchmarkRunRepository();
        await repository.AddAsync(run, CancellationToken.None);
        var engine = new FakeBenchmarkEngine
        {
            ResultToReturn = Result.Failure<BenchmarkEngineResult>(new Error("Benchmark.SidecarException", "UDS /var/run/bitcraft/sidecar.sock refused the connection."))
        };
        var handler = new BenchmarkFeatures.ProcessQueuedBenchmarkRunHandler(repository, engine);

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.ProcessQueuedBenchmarkRunCommand(run.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.SidecarException", result.Error.Code);
        Assert.Equal("Benchmark sidecar evaluation failed.", result.Error.Description);
        Assert.Equal("Benchmark sidecar evaluation failed.", run.FailureDescription);
        Assert.Equal("Benchmark sidecar evaluation failed.", run.ProgressMessage);
    }

    [Fact]
    public async Task ProcessQueuedBenchmarkRunHandler_WithMissingEngineCaseResult_MarksRunFailed()
    {
        // Arrange
        var runCase = new BenchmarkRunCase
        {
            CaseIndex = 0,
            InputText = "Question",
            ActualOutput = "Answer"
        };
        var run = new BenchmarkRun
        {
            TutorId = Guid.NewGuid(),
            BenchmarkTemplateId = Guid.NewGuid(),
            Status = BenchmarkRunStatus.Queued,
            Cases = [runCase]
        };

        var repository = new FakeBenchmarkRunRepository();
        await repository.AddAsync(run, CancellationToken.None);
        var engine = new FakeBenchmarkEngine
        {
            ResultToReturn = Result.Success(CreateBenchmarkEngineResult([]))
        };
        var handler = new BenchmarkFeatures.ProcessQueuedBenchmarkRunHandler(repository, engine);

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.ProcessQueuedBenchmarkRunCommand(run.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.EngineCaseResultsInvalid", result.Error.Code);
        Assert.Equal(BenchmarkRunStatus.Failed, run.Status);
        Assert.Null(runCase.Passed);
    }

    [Fact]
    public async Task ProcessQueuedBenchmarkRunHandler_WithDuplicateEngineCaseResult_MarksRunFailed()
    {
        // Arrange
        var runCase = new BenchmarkRunCase
        {
            CaseIndex = 0,
            InputText = "Question",
            ActualOutput = "Answer"
        };
        var run = new BenchmarkRun
        {
            TutorId = Guid.NewGuid(),
            BenchmarkTemplateId = Guid.NewGuid(),
            Status = BenchmarkRunStatus.Queued,
            Cases = [runCase]
        };

        var repository = new FakeBenchmarkRunRepository();
        await repository.AddAsync(run, CancellationToken.None);
        var duplicate = CreateBenchmarkCaseResult(runCase.Id, runCase.CaseIndex);
        var engine = new FakeBenchmarkEngine
        {
            ResultToReturn = Result.Success(CreateBenchmarkEngineResult([duplicate, duplicate]))
        };
        var handler = new BenchmarkFeatures.ProcessQueuedBenchmarkRunHandler(repository, engine);

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.ProcessQueuedBenchmarkRunCommand(run.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.EngineCaseResultsInvalid", result.Error.Code);
        Assert.Equal(BenchmarkRunStatus.Failed, run.Status);
        Assert.Null(runCase.Passed);
    }

    [Fact]
    public async Task ProcessQueuedBenchmarkRunHandler_WithUnexpectedEngineCaseResult_MarksRunFailed()
    {
        // Arrange
        var runCase = new BenchmarkRunCase
        {
            CaseIndex = 0,
            InputText = "Question",
            ActualOutput = "Answer"
        };
        var run = new BenchmarkRun
        {
            TutorId = Guid.NewGuid(),
            BenchmarkTemplateId = Guid.NewGuid(),
            Status = BenchmarkRunStatus.Queued,
            Cases = [runCase]
        };

        var repository = new FakeBenchmarkRunRepository();
        await repository.AddAsync(run, CancellationToken.None);
        var engine = new FakeBenchmarkEngine
        {
            ResultToReturn = Result.Success(CreateBenchmarkEngineResult([CreateBenchmarkCaseResult(Guid.NewGuid(), runCase.CaseIndex)]))
        };
        var handler = new BenchmarkFeatures.ProcessQueuedBenchmarkRunHandler(repository, engine);

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.ProcessQueuedBenchmarkRunCommand(run.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Benchmark.EngineCaseResultsInvalid", result.Error.Code);
        Assert.Equal(BenchmarkRunStatus.Failed, run.Status);
        Assert.Null(runCase.Passed);
    }

    [Fact]
    public async Task ProcessQueuedBenchmarkRunHandler_WithEngineSuccess_MapsSummaryAndCaseResults()
    {
        // Arrange
        var template = new BenchmarkTemplate
        {
            OwnerId = Guid.NewGuid(),
            Key = "benchmark-key",
            Name = "Benchmark Name",
            GuidancePath = "docs/benchmarking-templates.md",
            MetricsJson = "[]",
            IsEnabled = true
        };
        var runCase = new BenchmarkRunCase
        {
            CaseIndex = 0,
            InputText = "Question",
            ActualOutput = "Answer",
            ExpectedOutput = "Answer"
        };
        var run = new BenchmarkRun
        {
            TutorId = Guid.NewGuid(),
            BenchmarkTemplateId = template.Id,
            BenchmarkTemplate = template,
            ConsistencyRuns = 2,
            Status = BenchmarkRunStatus.Queued,
            Cases = [runCase]
        };

        var repository = new FakeBenchmarkRunRepository();
        await repository.AddAsync(run, CancellationToken.None);
        var engine = new FakeBenchmarkEngine
        {
            ResultToReturn = Result.Success(new BenchmarkEngineResult(
                "deepeval",
                "3.x",
                new BenchmarkEngineSummary(
                    0.95,
                    1.0,
                    42.5,
                    120.0,
                    8.0,
                    0.01,
                    "{\"average_score\":0.95}",
                    "{\"engine\":\"deepeval\"}"),
                [new BenchmarkEngineCaseResult(
                    runCase.Id,
                    runCase.CaseIndex,
                    true,
                    0.95,
                    "All metrics passed.",
                    "[{\"key\":\"faithfulness\"},{\"key\":\"answer_relevancy\"},{\"key\":\"hallucination\"}]",
                    "{\"latency_ms\":42.5}",
                    "{\"cpu_count\":8}")],
                "{\"raw\":true}"))
        };
        var handler = new BenchmarkFeatures.ProcessQueuedBenchmarkRunHandler(repository, engine);

        // Act
        var result = await handler.Handle(new BenchmarkFeatures.ProcessQueuedBenchmarkRunCommand(run.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(BenchmarkRunStatus.Completed, run.Status);
        Assert.Equal(run.Cases.Count, run.CompletedCaseCount);
        Assert.Equal(100, run.ProgressPercent);
        Assert.Equal("Completed", run.ProgressStage);
        Assert.Equal("deepeval", run.EngineName);
        Assert.Equal("3.x", run.EngineVersion);
        Assert.Equal(0.95, run.AverageScore);
        Assert.Equal(1.0, run.PassRate);
        Assert.Equal(42.5, run.TotalLatencyMs);
        Assert.Equal("{\"average_score\":0.95}", run.SummaryJson);
        Assert.Equal("{\"raw\":true}", run.RawResultJson);
        Assert.True(runCase.Passed);
        Assert.Equal(0.95, runCase.Score);
        Assert.Equal("All metrics passed.", runCase.Reason);
        Assert.NotNull(engine.LastRequest);
        Assert.Equal("benchmark-key", engine.LastRequest!.TemplateKey);
        Assert.Equal("docs/benchmarking-templates.md", engine.LastRequest.GuidancePath);
        Assert.Equal("[]", engine.LastRequest.MetricsJson);
        Assert.Equal(2, repository.UpdateCalls);
    }

    private static BenchmarkEngineResult CreateBenchmarkEngineResult(IReadOnlyList<BenchmarkEngineCaseResult> cases) =>
        new(
            "deepeval",
            "3.x",
            new BenchmarkEngineSummary(0.95, 1.0, 42.5, 120.0, 8.0, 0.01, "{\"average_score\":0.95}", "{\"engine\":\"deepeval\"}"),
            cases,
            "{\"raw\":true}");

    private static BenchmarkEngineCaseResult CreateBenchmarkCaseResult(Guid caseId, int caseIndex) =>
        new(
            caseId,
            caseIndex,
            true,
            0.95,
            "All metrics passed.",
            "[{\"key\":\"faithfulness\"}]",
            "{\"latency_ms\":42.5}",
            "{\"cpu_count\":8}");
}
