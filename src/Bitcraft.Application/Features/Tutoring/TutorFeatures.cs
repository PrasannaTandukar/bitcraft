using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>
/// Tutor lifecycle use-cases (create / update / read). Authorization is enforced via the
/// resource-membership reader (<see cref="ITutorMembershipReader"/>); the founding caller
/// becomes the sole <see cref="ResourceRole.Owner"/> at create time. (D2/D3.)
/// </summary>
public static class TutorFeatures
{
    // ---------- Create ----------
    public sealed record CreateTutorCommand(
        string Name,
        string? Description) : IRequest<Result<Tutor>>;

    public sealed class CreateTutorHandler(ITutorRepository tutorRepository, ICurrentUserContext currentUser)
        : IRequestHandler<CreateTutorCommand, Result<Tutor>>
    {
        public async Task<Result<Tutor>> Handle(CreateTutorCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Result.Failure<Tutor>(new Error("Tutor.NameRequired", "Tutor name is required."));
            }

            // D2/D3: founding Owner membership is created atomically with the Tutor row
            // via the Tutor.Create factory.
            var tutor = Tutor.Create(
                name: request.Name.Trim(),
                description: request.Description?.Trim(),
                foundingOwnerUserId: currentUser.UserId);

            var created = await tutorRepository.AddAsync(tutor, cancellationToken);
            return Result.Success(created);
        }
    }

    // ---------- Update ----------
    public sealed record UpdateTutorCommand(
        Guid TutorId,
        string Name,
        string? Description,
        TutorStatus Status,
        Guid? ActivePromptVersionId) : IRequest<Result<Tutor>>;

    public sealed class UpdateTutorHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<UpdateTutorCommand, Result<Tutor>>
    {
        public async Task<Result<Tutor>> Handle(UpdateTutorCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Result.Failure<Tutor>(new Error("Tutor.NameRequired", "Tutor name is required."));
            }

            var tutor = await tutorRepository.GetByIdAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<Tutor>(new Error("Tutor.NotFound", $"Tutor '{request.TutorId}' not found."));
            }

            // D2/D3: Update is a content mutation → Editor or Owner required.
            var role = await membership.GetRoleAsync(tutor.Id, currentUser.UserId, cancellationToken);
            if (role is not ResourceRole.Owner and not ResourceRole.Editor)
            {
                return Result.Failure<Tutor>(new Error("Tutor.Forbidden", "Tutor membership required."));
            }

            tutor.Name = request.Name.Trim();
            tutor.Description = request.Description?.Trim();
            tutor.Status = request.Status;
            tutor.ActivePromptVersionId = request.ActivePromptVersionId;

            await tutorRepository.UpdateAsync(tutor, cancellationToken);
            return Result.Success(tutor);
        }
    }

    // ---------- Get by Id ----------
    public sealed record GetTutorByIdQuery(Guid TutorId)
        : IRequest<Result<Tutor>>;

    public sealed class GetTutorByIdHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<GetTutorByIdQuery, Result<Tutor>>
    {
        public async Task<Result<Tutor>> Handle(GetTutorByIdQuery request, CancellationToken cancellationToken)
        {
            var tutor = await tutorRepository.GetByIdWithDetailsAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<Tutor>(new Error("Tutor.NotFound", $"Tutor '{request.TutorId}' not found."));
            }

            // D2/D3: Read requires any membership role (Auditor, Editor, or Owner).
            var role = await membership.GetRoleAsync(tutor.Id, currentUser.UserId, cancellationToken);
            if (role is null)
            {
                return Result.Failure<Tutor>(new Error("Tutor.Forbidden", "Tutor membership required."));
            }

            return Result.Success(tutor);
        }
    }

    // ---------- List by Owner ----------
    public sealed record ListTutorsQuery : IRequest<List<Tutor>>;

    public sealed class ListTutorsHandler(ITutorRepository tutorRepository, ICurrentUserContext currentUser)
        : IRequestHandler<ListTutorsQuery, List<Tutor>>
    {
        // D2/D3: lists tutors where the caller has any membership row. Repository layer
        // performs the membership join; this handler does not need a role filter.
        public Task<List<Tutor>> Handle(ListTutorsQuery request, CancellationToken cancellationToken) =>
            tutorRepository.ListByOwnerAsync(currentUser.UserId, cancellationToken);
    }
}
