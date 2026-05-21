using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Infrastructure.Identity;

public static class IdentityRoleSeeder
{
    internal const string AdminEmailEnvVar = "admin@bitcraft";
    internal const string AdminPasswordEnvVar = "Password123!";

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<AppRole>>();
        var logger = serviceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(IdentityRoleSeeder).FullName!);

        foreach (var roleName in AppRoles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var roleResult = await roleManager.CreateAsync(new AppRole(roleName));
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(" ", roleResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Failed to seed role '{roleName}': {errors}");
            }
        }

        // Bootstrap admin seeding is opt-in via environment variables only. No defaults,
        // no fallbacks — the deployment owner must set BITCRAFT_ADMIN_EMAIL and
        // BITCRAFT_ADMIN_PASSWORD explicitly. See HUMAN_DECISIONS D9 and Twelve-Factor III.
        var adminEmail = Environment.GetEnvironmentVariable(AdminEmailEnvVar);
        var adminPassword = Environment.GetEnvironmentVariable(AdminPasswordEnvVar);

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning(
                "Bootstrap admin seeding skipped: {EmailVar} and/or {PasswordVar} are not set. " +
                "Set both environment variables explicitly to provision the initial admin account.",
                AdminEmailEnvVar,
                AdminPasswordEnvVar);
            return;
        }

        var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new AppUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var createResult = await userManager.CreateAsync(admin, adminPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(" ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to seed admin user: {errors}");
            }
            await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        }
    }
}
