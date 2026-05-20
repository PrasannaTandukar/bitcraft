namespace Bitcraft.Application.Common.Security;

public static class AuthorizationPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string EditorOrAdmin = nameof(EditorOrAdmin);
    public const string AuditorOrAbove = nameof(AuditorOrAbove);
}
