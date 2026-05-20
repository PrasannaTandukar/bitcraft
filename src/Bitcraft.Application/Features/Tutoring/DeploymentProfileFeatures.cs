using System.Text.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>
/// Deployment profile use-cases. A tutor has at most one active profile;
/// upserts replace the existing profile in-place.
/// </summary>
public static class DeploymentProfileFeatures
{
    public sealed record SaveDeploymentProfileCommand(
        Guid TutorId,
        DeploymentTarget Target,
        string? ConfigurationJson) : IRequest<Result<DeploymentProfile>>;

    public sealed class SaveDeploymentProfileHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<SaveDeploymentProfileCommand, Result<DeploymentProfile>>
    {
        public async Task<Result<DeploymentProfile>> Handle(SaveDeploymentProfileCommand request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(request.ConfigurationJson))
            {
                try
                {
                    using var _ = JsonDocument.Parse(request.ConfigurationJson);
                }
                catch (JsonException)
                {
                    return Result.Failure<DeploymentProfile>(new Error("Deployment.ConfigInvalid", "Deployment configuration must be valid JSON."));
                }
            }

            var tutor = await tutorRepository.GetByIdAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<DeploymentProfile>(new Error("Tutor.NotFound", $"Tutor '{request.TutorId}' not found."));
            }

            // D2/D3: deployment profile is a configuration mutation → Editor or Owner.
            var role = await membership.GetRoleAsync(tutor.Id, currentUser.UserId, cancellationToken);
            if (role is not ResourceRole.Owner and not ResourceRole.Editor)
            {
                return Result.Failure<DeploymentProfile>(new Error("Tutor.Forbidden", "Tutor membership required."));
            }

            var profile = new DeploymentProfile
            {
                TutorId = request.TutorId,
                Target = request.Target,
                ConfigurationJson = request.ConfigurationJson
            };

            var saved = await tutorRepository.UpsertDeploymentProfileAsync(profile, cancellationToken);
            return Result.Success(saved);
        }
    }
}
