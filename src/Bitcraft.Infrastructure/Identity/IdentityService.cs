using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Bitcraft.Infrastructure.Identity;

public class IdentityService(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager, IConfiguration configuration)
    : IIdentityService, IRoleManagementService
{
    public async Task<Result<IReadOnlyList<UserRoleMembership>>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await userManager.Users.ToListAsync(cancellationToken);
        var memberships = new List<UserRoleMembership>();
        foreach (var user in users)
        {
            memberships.Add(await BuildMembershipAsync(user));
        }
        return Result.Success<IReadOnlyList<UserRoleMembership>>(memberships);
    }
    public async Task<AuthResult> RegisterAsync(string email, string password)
    {
        var user = new AppUser
        {
            UserName = email,
            Email = email
        };

        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            return new AuthResult { Succeeded = false, Errors = result.Errors.Select(e => e.Description).ToArray() };
        }

        var roles = await userManager.GetRolesAsync(user);

        return new AuthResult
        {
            UserId = user.Id.ToString(),
            Succeeded = true,
            Token = GenerateJwtToken(user, roles),
            Email = user.Email!,
            Roles = roles.ToArray()
        };
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null || !await userManager.CheckPasswordAsync(user, password))
        {
            return new AuthResult { Succeeded = false, Errors = new[] { "Invalid email or password." } };
        }

        var roles = await userManager.GetRolesAsync(user);

        return new AuthResult
        {
            UserId = user.Id.ToString(),
            Succeeded = true,
            Token = GenerateJwtToken(user, roles),
            Email = user.Email!,
            Roles = roles.ToArray()
        };
    }

    public async Task<Result<IReadOnlyList<string>>> ListRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await roleManager.Roles
            .Where(role => role.Name != null)
            .Select(role => role.Name!)
            .OrderBy(role => role)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<string>>(roles);
    }

    public async Task<Result<UserRoleMembership>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Failure<UserRoleMembership>(new Error("User.NotFound", $"User '{userId}' not found."));
        }

        return Result.Success(await BuildMembershipAsync(user));
    }

    public async Task<Result<UserRoleMembership>> AssignRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var normalizedRole = AppRoles.Normalize(role);
        if (normalizedRole is null)
        {
            return Result.Failure<UserRoleMembership>(new Error("Role.Invalid", "Role must be Admin, Editor, or Viewer."));
        }

        if (!await roleManager.RoleExistsAsync(normalizedRole))
        {
            return Result.Failure<UserRoleMembership>(new Error("Role.NotFound", $"Role '{normalizedRole}' has not been seeded."));
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Failure<UserRoleMembership>(new Error("User.NotFound", $"User '{userId}' not found."));
        }

        if (await userManager.IsInRoleAsync(user, normalizedRole))
        {
            return Result.Failure<UserRoleMembership>(new Error("Role.AlreadyAssigned", $"User already has role '{normalizedRole}'."));
        }

        var result = await userManager.AddToRoleAsync(user, normalizedRole);
        return result.Succeeded
            ? Result.Success(await BuildMembershipAsync(user))
            : Result.Failure<UserRoleMembership>(new Error("Role.AssignFailed", string.Join(" ", result.Errors.Select(error => error.Description))));
    }

    public async Task<Result<UserRoleMembership>> RevokeRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var normalizedRole = AppRoles.Normalize(role);
        if (normalizedRole is null)
        {
            return Result.Failure<UserRoleMembership>(new Error("Role.Invalid", "Role must be Admin, Editor, or Viewer."));
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Failure<UserRoleMembership>(new Error("User.NotFound", $"User '{userId}' not found."));
        }

        if (!await userManager.IsInRoleAsync(user, normalizedRole))
        {
            return Result.Failure<UserRoleMembership>(new Error("Role.NotAssigned", $"User does not have role '{normalizedRole}'."));
        }

        var result = await userManager.RemoveFromRoleAsync(user, normalizedRole);
        return result.Succeeded
            ? Result.Success(await BuildMembershipAsync(user))
            : Result.Failure<UserRoleMembership>(new Error("Role.RevokeFailed", string.Join(" ", result.Errors.Select(error => error.Description))));
    }

    private async Task<UserRoleMembership> BuildMembershipAsync(AppUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new UserRoleMembership(user.Id, user.Email ?? string.Empty, roles.OrderBy(role => role).ToArray());
    }

    private string GenerateJwtToken(AppUser user, IEnumerable<string> roles)
    {
        var jwtSecret = configuration["JwtSettings:Secret"] ?? "super_secret_key_used_for_local_dev_only_should_be_in_env";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
