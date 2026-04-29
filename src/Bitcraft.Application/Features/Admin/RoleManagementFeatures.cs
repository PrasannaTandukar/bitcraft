using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using MediatR;

namespace Bitcraft.Application.Features.Admin;

public static class RoleManagementFeatures
{
    public sealed record ListRolesQuery : IRequest<Result<IReadOnlyList<string>>>;

    public sealed class ListRolesHandler(IRoleManagementService roleManagement, ICurrentUserContext currentUser)
        : IRequestHandler<ListRolesQuery, Result<IReadOnlyList<string>>>
    {
        public Task<Result<IReadOnlyList<string>>> Handle(ListRolesQuery request, CancellationToken cancellationToken) =>
            currentUser.IsInRole(AppRoles.Admin)
                ? roleManagement.ListRolesAsync(cancellationToken)
                : Task.FromResult(Result.Failure<IReadOnlyList<string>>(Forbidden));
    }

    public sealed record GetUserRolesQuery(Guid UserId) : IRequest<Result<UserRoleMembership>>;

    public sealed class GetUserRolesHandler(IRoleManagementService roleManagement, ICurrentUserContext currentUser)
        : IRequestHandler<GetUserRolesQuery, Result<UserRoleMembership>>
    {
        public Task<Result<UserRoleMembership>> Handle(GetUserRolesQuery request, CancellationToken cancellationToken) =>
            currentUser.IsInRole(AppRoles.Admin)
                ? roleManagement.GetUserRolesAsync(request.UserId, cancellationToken)
                : Task.FromResult(Result.Failure<UserRoleMembership>(Forbidden));
    }

    public sealed record AssignUserRoleCommand(Guid UserId, string Role) : IRequest<Result<UserRoleMembership>>;

    public sealed class AssignUserRoleHandler(IRoleManagementService roleManagement, ICurrentUserContext currentUser)
        : IRequestHandler<AssignUserRoleCommand, Result<UserRoleMembership>>
    {
        public Task<Result<UserRoleMembership>> Handle(AssignUserRoleCommand request, CancellationToken cancellationToken) =>
            currentUser.IsInRole(AppRoles.Admin)
                ? roleManagement.AssignRoleAsync(request.UserId, request.Role, cancellationToken)
                : Task.FromResult(Result.Failure<UserRoleMembership>(Forbidden));
    }

    public sealed record RevokeUserRoleCommand(Guid UserId, string Role) : IRequest<Result<UserRoleMembership>>;

    public sealed class RevokeUserRoleHandler(IRoleManagementService roleManagement, ICurrentUserContext currentUser)
        : IRequestHandler<RevokeUserRoleCommand, Result<UserRoleMembership>>
    {
        public Task<Result<UserRoleMembership>> Handle(RevokeUserRoleCommand request, CancellationToken cancellationToken) =>
            currentUser.IsInRole(AppRoles.Admin)
                ? roleManagement.RevokeRoleAsync(request.UserId, request.Role, cancellationToken)
                : Task.FromResult(Result.Failure<UserRoleMembership>(Forbidden));
    }

    private static readonly Error Forbidden = new("RoleManagement.Forbidden", "Only administrators can manage user roles.");
}
