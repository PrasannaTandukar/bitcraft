using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Common.Interfaces;

/// <summary>Executes one stateless tutor turn. Callers resolve authorization, snapshots and attachments.</summary>
public interface ITutorAgentExecutor
{
    Task<Result<TutorAgentResponse>> ExecuteAsync(
        TutorAgentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TutorAgentResponse>> ExecuteStreamingAsync(
        TutorAgentRequest request,
        Func<TutorAgentStreamEvent, CancellationToken, ValueTask> observer,
        CancellationToken cancellationToken = default);
}
