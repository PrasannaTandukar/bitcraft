namespace Bitcraft.Api.Contracts.Admin;

/// <summary>Role available for API authorization policies.</summary>
public sealed record RoleResponse(string Name);

/// <summary>Request to assign a role to a user.</summary>
public sealed record AssignUserRoleRequest(string Role);

/// <summary>Current roles assigned to a user.</summary>
public sealed record UserRoleMembershipResponse(Guid UserId, string Email, IReadOnlyList<string> Roles);
