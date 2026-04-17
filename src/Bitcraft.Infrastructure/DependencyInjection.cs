using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Infrastructure.Persistence;
using Bitcraft.Infrastructure.Persistence.Repositories.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bitcraft.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                o => o.UseVector()));

        services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();

        return services;
    }
}
