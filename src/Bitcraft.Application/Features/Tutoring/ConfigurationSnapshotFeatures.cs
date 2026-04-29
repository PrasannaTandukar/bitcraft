using System.Text.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>
/// Configuration snapshot use-cases. A snapshot captures the immutable
/// configuration state of a tutor at a point in time, used by Phase C
/// benchmarking to reproduce evaluation runs.
/// </summary>
public static class ConfigurationSnapshotFeatures
{
    public sealed record CreateConfigurationSnapshotCommand(
        Guid TutorId) : IRequest<Result<TutorConfigurationSnapshot>>;

    public sealed class CreateConfigurationSnapshotHandler(
        ITutorRepository tutorRepository,
        ICurrentUserContext currentUser)
        : IRequestHandler<CreateConfigurationSnapshotCommand, Result<TutorConfigurationSnapshot>>
    {
        public async Task<Result<TutorConfigurationSnapshot>> Handle(
            CreateConfigurationSnapshotCommand request,
            CancellationToken cancellationToken)
        {
            var tutor = await tutorRepository.GetByIdWithDetailsAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<TutorConfigurationSnapshot>(new Error("Tutor.NotFound", $"Tutor '{request.TutorId}' not found."));
            }

            var userId = currentUser.UserId;
            if (tutor.OwnerId != userId)
            {
                return Result.Failure<TutorConfigurationSnapshot>(new Error("Tutor.Forbidden", "Requesting user does not own this tutor."));
            }

            var promptVersion = tutor.ActivePromptVersion
                ?? tutor.PromptVersions.FirstOrDefault(v => v.Id == tutor.ActivePromptVersionId);

            var toolsJson = tutor.Tools.Count == 0
                ? null
                : JsonSerializer.Serialize(tutor.Tools.Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Description,
                    t.JsonSchema,
                    t.ExecutionUrl
                }));

            var knowledgeJson = tutor.KnowledgeSources.Count == 0
                ? null
                : JsonSerializer.Serialize(tutor.KnowledgeSources.Select(k => new
                {
                    k.Id,
                    k.DocumentId,
                    k.LinkedUtc
                }));

            var snapshot = TutorConfigurationSnapshot.Capture(
                tutor,
                promptVersion,
                tutor.TutorModelConnection,
                toolsJson,
                knowledgeJson,
                tutor.DeploymentProfile,
                userId);

            var saved = await tutorRepository.AddSnapshotAsync(snapshot, cancellationToken);
            return Result.Success(saved);
        }
    }
}
