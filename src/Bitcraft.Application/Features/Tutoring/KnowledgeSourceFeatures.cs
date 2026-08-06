using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>
/// Use-cases for linking knowledge documents to a tutor.
/// </summary>
public static class KnowledgeSourceFeatures
{
    public sealed record LinkKnowledgeSourceCommand(
        Guid TutorId,
        Guid DocumentId) : IRequest<Result<KnowledgeSourceLink>>;

    public sealed class LinkKnowledgeSourceHandler(
        ITutorRepository tutorRepository,
        IDocumentStore documentStore,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<LinkKnowledgeSourceCommand, Result<KnowledgeSourceLink>>
    {
        public async Task<Result<KnowledgeSourceLink>> Handle(LinkKnowledgeSourceCommand request, CancellationToken cancellationToken)
        {
            var tutor = await tutorRepository.GetByIdAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<KnowledgeSourceLink>(new Error("Tutor.NotFound", $"Tutor '{request.TutorId}' not found."));
            }

            // D2/D3: linking knowledge mutates tutor content → Editor or Owner.
            var role = await membership.GetRoleAsync(tutor.Id, currentUser.UserId, cancellationToken);
            if (role is not ResourceRole.Owner and not ResourceRole.Editor)
            {
                return Result.Failure<KnowledgeSourceLink>(new Error("Tutor.Forbidden", "Tutor membership required."));
            }

            var document = await documentStore.GetByIdAsync(request.DocumentId, cancellationToken);
            if (document is null)
            {
                return Result.Failure<KnowledgeSourceLink>(new Error("Document.NotFound", $"Document '{request.DocumentId}' not found."));
            }

            if (document.CreatedById != currentUser.UserId)
            {
                return Result.Failure<KnowledgeSourceLink>(new Error("Document.Forbidden", "Requesting user does not own this document."));
            }

            if (await tutorRepository.KnowledgeSourceLinkExistsAsync(request.TutorId, request.DocumentId, cancellationToken))
            {
                return Result.Failure<KnowledgeSourceLink>(new Error("KnowledgeSource.AlreadyLinked", "Document is already linked to this tutor."));
            }

            var link = new KnowledgeSourceLink
            {
                TutorId = request.TutorId,
                DocumentId = request.DocumentId
            };

            var created = await tutorRepository.AddKnowledgeSourceLinkAsync(link, cancellationToken);
            return Result.Success(created);
        }
    }
}
