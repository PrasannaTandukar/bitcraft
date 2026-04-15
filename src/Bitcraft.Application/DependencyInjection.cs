using Bitcraft.Application.Features.Chunks.CreateChunk;
using Bitcraft.Application.Features.Chunks.SearchChunks;
using Microsoft.Extensions.DependencyInjection;

namespace Bitcraft.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateChunkHandler>();
        services.AddScoped<SearchChunksHandler>();

        return services;
    }
}
