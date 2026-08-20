using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Common.Interfaces;

public interface IRoleManagementService
{
    Task<Result<IReadOnlyList<UserRoleMembership>>> ListUsersAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<string>>> ListRolesAsync(CancellationToken cancellationToken = default);
    Task<Result<UserRoleMembership>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<UserRoleMembership>> AssignRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<UserRoleMembership>> RevokeRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<UserRoleMembership>> CreateUserAsync(string email, string password, IReadOnlyList<string>? initialRoles = null, CancellationToken cancellationToken = default);
}
