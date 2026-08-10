using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Modeling;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>Read-only model catalog queries used while configuring a tutor revision.</summary>
public static class ModelCatalogFeatures
{
    private static readonly Error Forbidden = new("ModelCatalog.Forbidden", "Tutor membership required.");

    public sealed record ListInteractionModelsQuery(Guid TutorId, bool IncludeDisabled = false)
        : IRequest<Result<IReadOnlyList<InteractionModel>>>;

    public sealed class ListInteractionModelsHandler(
        ITutorRepository tutors,
        IModelRepository models,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListInteractionModelsQuery, Result<IReadOnlyList<InteractionModel>>>
    {
        public async Task<Result<IReadOnlyList<InteractionModel>>> Handle(
            ListInteractionModelsQuery request,
            CancellationToken cancellationToken)
        {
            if (await tutors.GetByIdAsync(request.TutorId, cancellationToken) is null)
            {
                return Result.Failure<IReadOnlyList<InteractionModel>>(new Error("Tutor.NotFound", "Tutor not found."));
            }

            if (await membership.GetRoleAsync(request.TutorId, currentUser.UserId, cancellationToken) is null)
            {
                return Result.Failure<IReadOnlyList<InteractionModel>>(Forbidden);
            }

            var includeDisabled = request.IncludeDisabled && currentUser.IsInRole(AppRoles.Admin);
            var available = await models.ListInteractionModelsAsync(cancellationToken);
            var result = available
                .Where(x => includeDisabled || (x.IsEnabled && x.RuntimeEngine?.IsEnabled == true))
                .ToList();
            return Result.Success<IReadOnlyList<InteractionModel>>(result);
        }
    }

    public sealed record ListEmbeddingModelsQuery(Guid? TutorId = null, bool IncludeDisabled = false)
        : IRequest<Result<IReadOnlyList<EmbeddingModel>>>;

    public sealed class ListEmbeddingModelsHandler(
        ITutorRepository tutors,
        IModelRepository models,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListEmbeddingModelsQuery, Result<IReadOnlyList<EmbeddingModel>>>
    {
        public async Task<Result<IReadOnlyList<EmbeddingModel>>> Handle(
            ListEmbeddingModelsQuery request,
            CancellationToken cancellationToken)
        {
            if (request.TutorId is { } tutorId && await tutors.GetByIdAsync(tutorId, cancellationToken) is null)
            {
                return Result.Failure<IReadOnlyList<EmbeddingModel>>(new Error("Tutor.NotFound", "Tutor not found."));
            }

            if (request.TutorId is { } scopedTutorId &&
                await membership.GetRoleAsync(scopedTutorId, currentUser.UserId, cancellationToken) is null)
            {
                return Result.Failure<IReadOnlyList<EmbeddingModel>>(Forbidden);
            }

            if (request.TutorId is null &&
                !currentUser.IsInRole(AppRoles.Editor) &&
                !currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<IReadOnlyList<EmbeddingModel>>(new Error(
                    "ModelCatalog.Forbidden",
                    "Editor or Admin access is required to list document embedding models."));
            }

            var includeDisabled = request.IncludeDisabled && currentUser.IsInRole(AppRoles.Admin);
            var available = await models.ListEmbeddingModelsAsync(cancellationToken);
            var result = available
                .Where(x => includeDisabled || (x.IsEnabled && x.RuntimeEngine?.IsEnabled == true))
                .ToList();
            return Result.Success<IReadOnlyList<EmbeddingModel>>(result);
        }
    }
}
