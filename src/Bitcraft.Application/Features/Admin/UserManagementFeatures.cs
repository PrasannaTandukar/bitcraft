using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
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
}