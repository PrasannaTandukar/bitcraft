using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>
/// Prompt version use-cases. Versions are append-only and assigned a
/// monotonically increasing number per tutor.
/// </summary>
public static class PromptVersionFeatures
{
    public sealed record CreatePromptVersionCommand(
        Guid TutorId,
        string Name,
        string Content,
        string? Notes,
        bool SetAsActive) : IRequest<Result<PromptVersion>>;

    public sealed class CreatePromptVersionHandler(
        ITutorRepository tutorRepository,
        ITutorMembershipReader membership,
        ICurrentUserContext currentUser)
        : IRequestHandler<CreatePromptVersionCommand, Result<PromptVersion>>
    {
        public async Task<Result<PromptVersion>> Handle(CreatePromptVersionCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Result.Failure<PromptVersion>(new Error("PromptVersion.NameRequired", "Prompt version name is required."));
            }

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Result.Failure<PromptVersion>(new Error("PromptVersion.ContentRequired", "Prompt content is required."));
            }

            var tutor = await tutorRepository.GetByIdAsync(request.TutorId, cancellationToken);
            if (tutor is null)
            {
                return Result.Failure<PromptVersion>(new Error("Tutor.NotFound", $"Tutor '{request.TutorId}' not found."));
            }

            // D2/D3: creating prompt versions is a content mutation → Editor or Owner.
            var role = await membership.GetRoleAsync(tutor.Id, currentUser.UserId, cancellationToken);
            if (role is not ResourceRole.Owner and not ResourceRole.Editor)
            {
                return Result.Failure<PromptVersion>(new Error("Tutor.Forbidden", "Tutor membership required."));
            }

            var nextNumber = await tutorRepository.GetNextPromptVersionNumberAsync(request.TutorId, cancellationToken);

            var version = new PromptVersion
            {
                TutorId = request.TutorId,
                VersionNumber = nextNumber,
                Name = request.Name.Trim(),
                Content = request.Content,
                Notes = request.Notes
            };

            var created = await tutorRepository.AddPromptVersionAsync(version, cancellationToken);

            if (request.SetAsActive)
            {
                tutor.ActivePromptVersionId = created.Id;
                await tutorRepository.UpdateAsync(tutor, cancellationToken);
            }

            return Result.Success(created);
        }
    }
}
