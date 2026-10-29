using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using MediatR;

namespace Bitcraft.Application.Features.Admin;

public static class UserManagementFeatures
{
    public record ListUsersQuery(string? ExcludeUserId = null) : IRequest<Result<IReadOnlyList<UserRoleMembership>>>;

    public class ListUsersHandler(IRoleManagementService roleManagement)
        : IRequestHandler<ListUsersQuery, Result<IReadOnlyList<UserRoleMembership>>>
    {
        public async Task<Result<IReadOnlyList<UserRoleMembership>>> Handle(
            ListUsersQuery request, CancellationToken cancellationToken)
        {
            var result = await roleManagement.ListUsersAsync(cancellationToken);

            if (!result.IsSuccess || string.IsNullOrEmpty(request.ExcludeUserId))
                return result;

            var filtered = Guid.TryParse(request.ExcludeUserId, out var excludeGuid)
    ? result.Value.Where(u => u.UserId != excludeGuid).ToList()
    : result.Value.ToList();

            return Result.Success<IReadOnlyList<UserRoleMembership>>(filtered);
        }
    }

    public sealed record CreateUserCommand(
        string Email,
        string Password,
        IReadOnlyList<string>? InitialRoles = null) : IRequest<Result<UserRoleMembership>>;

    public sealed class CreateUserHandler(
        IRoleManagementService roleManagement,
        ICurrentUserContext currentUser)
        : IRequestHandler<CreateUserCommand, Result<UserRoleMembership>>
    {
        public async Task<Result<UserRoleMembership>> Handle(
            CreateUserCommand request,
            CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<UserRoleMembership>(
                    new Error("UserManagement.Forbidden", "Only administrators can create users."));
            }

            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            {
                return Result.Failure<UserRoleMembership>(
                    new Error("UserManagement.EmailInvalid", "A valid email address is required."));
            }

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            {
                return Result.Failure<UserRoleMembership>(
                    new Error("UserManagement.PasswordInvalid", "Password must be at least 6 characters."));
            }

            return await roleManagement.CreateUserAsync(
                request.Email.Trim(),
                request.Password,
                request.InitialRoles,
                cancellationToken);
        }
    }
}