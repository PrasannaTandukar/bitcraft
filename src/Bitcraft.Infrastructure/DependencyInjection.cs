using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Identity;
using Bitcraft.Infrastructure.Identity;
using Bitcraft.Infrastructure.Persistence;
using Bitcraft.Infrastructure.Persistence.Repositories.Knowledge;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Bitcraft.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                o => o.UseVector()));

        services.AddIdentity<AppUser, AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var jwtSecret = configuration["JwtSettings:Secret"] ?? "super_secret_key_used_for_local_dev_only_should_be_in_env";
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            };
        });

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();

        return services;
    }
}
