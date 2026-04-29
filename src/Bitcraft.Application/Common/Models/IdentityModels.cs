namespace Bitcraft.Application.Common.Models;

public class AuthResult
{
    public bool Succeeded { get; set; }
    public string[] Errors { get; set; } = Array.Empty<string>();
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public sealed record UserRoleMembership(Guid UserId, string Email, IReadOnlyList<string> Roles);
