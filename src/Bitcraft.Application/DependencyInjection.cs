using System.Reflection;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Features.Tutoring;
using Microsoft.Extensions.DependencyInjection;

namespace Bitcraft.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddScoped<ITutorRuntime, TutorRuntime>();

        return services;
    }
}
