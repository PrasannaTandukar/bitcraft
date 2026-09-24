using System.Text.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>
/// Configuration snapshot use-cases. A snapshot captures the immutable
/// configuration state of a tutor at a point in time, used by benchmarking
/// to reproduce evaluation runs.
/// </summary>
public static class ConfigurationSnapshotFeatures
{
    private static readonly Error NotFound = new("TutorConfigurationSnapshot.NotFound", "Tutor configuration snapshot not found.");
    private static readonly Error Forbidden = new("TutorConfigurationSnapshot.Forbidden", "Tutor membership required.");

    public sealed record ListConfigurationSnapshotsQuery(Guid TutorId)
        : IRequest<Result<IReadOnlyList<TutorConfigurationSnapshot>>>;

    public sealed class ListConfigurationSnapshotsHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListConfigurationSnapshotsQuery, Result<IReadOnlyList<TutorConfigurationSnapshot>>>
    {
        public async Task<Result<IReadOnlyList<TutorConfigurationSnapshot>>> Handle(
            ListConfigurationSnapshotsQuery request,
            CancellationToken cancellationToken)
        {
            var tutor = await tutorRepository.GetByIdAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<IReadOnlyList<TutorConfigurationSnapshot>>(new Error("Tutor.NotFound", "Tutor not found."));
            }

            var role = await membership.GetRoleAsync(request.TutorId, currentUser.UserId, cancellationToken);
            if (role is null)
            {
                return Result.Failure<IReadOnlyList<TutorConfigurationSnapshot>>(Forbidden);
            }

            var snapshots = await tutorRepository.ListSnapshotsByTutorAsync(request.TutorId, cancellationToken);
            return Result.Success<IReadOnlyList<TutorConfigurationSnapshot>>(snapshots);
        }
    }

    public sealed record GetConfigurationSnapshotQuery(Guid TutorId, Guid SnapshotId)
        : IRequest<Result<TutorConfigurationSnapshot>>;

    public sealed class GetConfigurationSnapshotHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<GetConfigurationSnapshotQuery, Result<TutorConfigurationSnapshot>>
    {
        public async Task<Result<TutorConfigurationSnapshot>> Handle(
            GetConfigurationSnapshotQuery request,
            CancellationToken cancellationToken)
        {
            var snapshot = await tutorRepository.GetSnapshotByIdAsync(request.SnapshotId, cancellationToken);
            if (snapshot is null || snapshot.TutorId != request.TutorId)
            {
                return Result.Failure<TutorConfigurationSnapshot>(NotFound);
            }

            var role = await membership.GetRoleAsync(request.TutorId, currentUser.UserId, cancellationToken);
            if (role is null)
            {
                return Result.Failure<TutorConfigurationSnapshot>(Forbidden);
            }

            return Result.Success(snapshot);
        }
    }

    public sealed record CreateConfigurationSnapshotCommand(
        Guid TutorId) : IRequest<Result<TutorConfigurationSnapshot>>;

    public sealed class CreateConfigurationSnapshotHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
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
            // D2/D3: capturing a snapshot is an immutable record of current config → Editor or Owner.
            var role = await membership.GetRoleAsync(tutor.Id, userId, cancellationToken);
            if (role is not ResourceRole.Owner and not ResourceRole.Editor)
            {
                return Result.Failure<TutorConfigurationSnapshot>(new Error("Tutor.Forbidden", "Tutor membership required."));
            }

            var promptVersion = tutor.ActivePromptVersion
                ?? tutor.PromptVersions.FirstOrDefault(v => v.Id == tutor.ActivePromptVersionId);

            var toolsJson = tutor.Tools.Count == 0
                ? null
                : JsonSerializer.Serialize(tutor.Tools.Select(t => new
                {
                    t.Id,
                    t.TutorId,
                    t.Name,
                    t.Description,
                    t.JsonSchema,
                    t.Kind,
                    t.ImplementationKey,
                    t.ExecutionPolicy
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
                await tutorRepository.GetNextSnapshotVersionNumberAsync(tutor.Id, cancellationToken),
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
