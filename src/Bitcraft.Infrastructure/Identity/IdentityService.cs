using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Bitcraft.Infrastructure.Identity;

public class IdentityService(UserManager<AppUser> userManager, IConfiguration configuration) : IIdentityService
{
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

        return new AuthResult
        {
            Succeeded = true,
            Token = GenerateJwtToken(user),
            Email = user.Email!
        };
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null || !await userManager.CheckPasswordAsync(user, password))
        {
            return new AuthResult { Succeeded = false, Errors = new[] { "Invalid email or password." } };
        }

        return new AuthResult
        {
            Succeeded = true,
            Token = GenerateJwtToken(user),
            Email = user.Email!
        };
    }

    private string GenerateJwtToken(AppUser user)
    {
        var jwtSecret = configuration["JwtSettings:Secret"] ?? "super_secret_key_used_for_local_dev_only_should_be_in_env";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
