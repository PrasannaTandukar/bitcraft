using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>
/// Tutor lifecycle use-cases (create / update / read). Authorization
/// is enforced by comparing <see cref="Tutor.OwnerId"/> with the current user.
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

            var tutor = new Tutor
            {
                OwnerId = currentUser.UserId,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Status = TutorStatus.Draft
            };

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

    public sealed class UpdateTutorHandler(ITutorRepository tutorRepository, ICurrentUserContext currentUser)
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

            if (tutor.OwnerId != currentUser.UserId)
            {
                return Result.Failure<Tutor>(new Error("Tutor.Forbidden", "Requesting user does not own this tutor."));
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

    public sealed class GetTutorByIdHandler(ITutorRepository tutorRepository, ICurrentUserContext currentUser)
        : IRequestHandler<GetTutorByIdQuery, Result<Tutor>>
    {
        public async Task<Result<Tutor>> Handle(GetTutorByIdQuery request, CancellationToken cancellationToken)
        {
            var tutor = await tutorRepository.GetByIdWithDetailsAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<Tutor>(new Error("Tutor.NotFound", $"Tutor '{request.TutorId}' not found."));
            }

            if (tutor.OwnerId != currentUser.UserId)
            {
                return Result.Failure<Tutor>(new Error("Tutor.Forbidden", "Requesting user does not own this tutor."));
            }

            return Result.Success(tutor);
        }
    }

    // ---------- List by Owner ----------
    public sealed record ListTutorsQuery : IRequest<List<Tutor>>;

    public sealed class ListTutorsHandler(ITutorRepository tutorRepository, ICurrentUserContext currentUser)
        : IRequestHandler<ListTutorsQuery, List<Tutor>>
    {
        public Task<List<Tutor>> Handle(ListTutorsQuery request, CancellationToken cancellationToken) =>
            tutorRepository.ListByOwnerAsync(currentUser.UserId, cancellationToken);
    }
}
