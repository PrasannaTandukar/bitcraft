namespace Bitcraft.Application.Common.Models;

public class AuthResult
{
    public bool Succeeded { get; set; }
    public string[] Errors { get; set; } = Array.Empty<string>();
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? InstitutionId { get; set; }
}
