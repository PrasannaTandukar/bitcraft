using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using MediatR;

namespace Bitcraft.Application.Features.Admin;

public static class UserManagementFeatures
{
    public record ListUsersQuery : IRequest<Result<IReadOnlyList<UserRoleMembership>>>;

    public class ListUsersHandler(IRoleManagementService roleManagement)
        : IRequestHandler<ListUsersQuery, Result<IReadOnlyList<UserRoleMembership>>>
    {
        public async Task<Result<IReadOnlyList<UserRoleMembership>>> Handle(
            ListUsersQuery request, CancellationToken cancellationToken)
        {
            return await roleManagement.ListUsersAsync(cancellationToken);
        }
    }
}
