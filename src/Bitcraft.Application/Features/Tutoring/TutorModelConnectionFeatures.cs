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
        Guid ModelCatalogEntryId,
        string? OverrideSettingsJson) : IRequest<Result<TutorModelConnection>>;

    public sealed class SaveTutorModelConnectionHandler(
        ITutorRepository tutorRepository,
        IModelCatalogRepository modelCatalogRepository,
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

            if (tutor.OwnerId != userId)
            {
                return Result.Failure<TutorModelConnection>(new Error("Tutor.Forbidden", "Requesting user does not own this tutor."));
            }

            var catalogEntry = await modelCatalogRepository.GetByIdAsync(request.ModelCatalogEntryId, cancellationToken);
            if (catalogEntry is null)
            {
                return Result.Failure<TutorModelConnection>(new Error("ModelCatalog.NotFound", $"Model catalog entry '{request.ModelCatalogEntryId}' not found."));
            }

            if (!catalogEntry.IsEnabled)
            {
                return Result.Failure<TutorModelConnection>(new Error("ModelCatalog.Disabled", "Model catalog entry is disabled."));
            }

            var connection = new TutorModelConnection
            {
                TutorId = request.TutorId,
                ModelCatalogEntryId = request.ModelCatalogEntryId,
                OverrideSettingsJson = string.IsNullOrWhiteSpace(request.OverrideSettingsJson) ? null : request.OverrideSettingsJson
            };

            var saved = await tutorRepository.UpsertTutorModelConnectionAsync(connection, cancellationToken);
            return Result.Success(saved);
        }
    }
}