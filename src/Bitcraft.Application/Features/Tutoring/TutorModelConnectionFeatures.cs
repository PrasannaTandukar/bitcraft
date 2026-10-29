using System.Text.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

public static class TutorModelConnectionFeatures
{
    public sealed record SaveTutorModelConnectionCommand(
        Guid TutorId,
        Guid InteractionModelId,
        string? OverrideSettingsJson) : IRequest<Result<TutorModelConnection>>;

    public sealed class SaveTutorModelConnectionHandler(
        ITutorRepository tutorRepository,
        IModelRepository modelRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<SaveTutorModelConnectionCommand, Result<TutorModelConnection>>
    {
        public async Task<Result<TutorModelConnection>> Handle(SaveTutorModelConnectionCommand request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(request.OverrideSettingsJson))
            {
                try
                {
                    using var _ = JsonDocument.Parse(request.OverrideSettingsJson);
                }
                catch (JsonException)
                {
                    return Result.Failure<TutorModelConnection>(new Error("TutorModelConnection.OverrideSettingsInvalid", "Override settings must be valid JSON."));
                }
            }

            var userId = currentUser.UserId;
            var tutor = await tutorRepository.GetByIdAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<TutorModelConnection>(new Error("Tutor.NotFound", $"Tutor '{request.TutorId}' not found."));
            }

            // D2/D3: model connection mutates tutor config → Editor or Owner.
            var role = await membership.GetRoleAsync(tutor.Id, userId, cancellationToken);
            if (role is not ResourceRole.Owner and not ResourceRole.Editor)
            {
                return Result.Failure<TutorModelConnection>(new Error("Tutor.Forbidden", "Tutor membership required."));
            }

            var interactionModel = await modelRepository.GetInteractionModelByIdAsync(request.InteractionModelId, cancellationToken);
            if (interactionModel is null)
            {
                return Result.Failure<TutorModelConnection>(new Error("InteractionModel.NotFound", $"Interaction model '{request.InteractionModelId}' not found."));
            }

            if (!interactionModel.IsEnabled)
            {
                return Result.Failure<TutorModelConnection>(new Error("InteractionModel.Disabled", "Interaction model is disabled."));
            }

            var connection = new TutorModelConnection
            {
                TutorId = request.TutorId,
                InteractionModelId = request.InteractionModelId,
                OverrideSettingsJson = string.IsNullOrWhiteSpace(request.OverrideSettingsJson) ? null : request.OverrideSettingsJson
            };

            var saved = await tutorRepository.UpsertTutorModelConnectionAsync(connection, cancellationToken);
            return Result.Success(saved);
        }
    }
}