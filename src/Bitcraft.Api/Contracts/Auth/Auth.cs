namespace Bitcraft.Api.Contracts.Auth;

/// <summary>
/// Request payload for registering a new user.
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// User's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's secure password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

}

/// <summary>
/// Request payload for logging in.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// User's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's secure password.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Response payload containing the JWT and user information.
/// </summary>
public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    public string UserId { get; set; } = string.Empty;
}
