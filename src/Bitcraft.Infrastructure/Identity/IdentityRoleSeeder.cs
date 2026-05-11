using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Bitcraft.Infrastructure.Identity;

public static class IdentityRoleSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<AppRole>>();

        foreach (var roleName in AppRoles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new AppRole(roleName));
            if (!result.Succeeded)
            {
                var errors = string.Join(" ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Failed to seed role '{roleName}': {errors}");
            }
        }
        // Seed default admin user
        var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();
        const string adminEmail = "admin@bitcraft.com";
        const string adminPassword = "Admin1234!";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new AppUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var result = await userManager.CreateAsync(admin, adminPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(" ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to seed admin user: {errors}");
            }
            await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        }
    }
}
