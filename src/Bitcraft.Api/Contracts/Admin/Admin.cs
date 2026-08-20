using Bitcraft.Application.Common.Models;

namespace Bitcraft.Api.Contracts.Admin;

public class UserResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    public UserResponse(UserRoleMembership membership)
    {
        UserId = membership.UserId;
        Email = membership.Email;
        Roles = membership.Roles;
    }
}

/// <summary>Request to create a new user in the admin workspace.</summary>
public sealed record CreateUserRequest(
    string Email,
    string Password,
    IReadOnlyList<string>? Roles = null);
