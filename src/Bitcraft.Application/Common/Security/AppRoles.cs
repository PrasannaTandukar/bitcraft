namespace Bitcraft.Application.Common.Security;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Auditor = "Auditor";

    public static readonly IReadOnlyCollection<string> All = [Admin, Editor, Auditor];

    public static bool IsSupported(string? role) => Normalize(role) is not null;

    public static string? Normalize(string? role) => string.IsNullOrWhiteSpace(role)
        ? null
        : All.FirstOrDefault(x => string.Equals(x, role.Trim(), StringComparison.OrdinalIgnoreCase));
}
