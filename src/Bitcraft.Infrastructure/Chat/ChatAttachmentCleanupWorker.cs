using Bitcraft.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Infrastructure.Chat;

public sealed class ChatAttachmentCleanupWorker(IServiceScopeFactory scopes, ILogger<ChatAttachmentCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IChatAttachmentService>().CleanupExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // Do not include exception messages that might contain storage paths or connection details.
                logger.LogWarning("Chat attachment cleanup failed; it will retry on the next sweep.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
