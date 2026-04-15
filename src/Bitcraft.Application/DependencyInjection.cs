using Bitcraft.Application.Features.Knowledge.Commands;
using Bitcraft.Application.Features.Knowledge.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Bitcraft.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateDocumentChunkHandler>();
        services.AddScoped<SearchKnowledgeHandler>();

        return services;
    }
}
