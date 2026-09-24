using Bitcraft.Application.Common.Models;
using System.Runtime.CompilerServices;

namespace Bitcraft.Application.Common.Interfaces;

public interface ITutorRuntime
{
    Task<Result<TutorRuntimeResult>> ExecuteAsync(
        TutorRuntimeRequest request,
        CancellationToken cancellationToken = default);

    async IAsyncEnumerable<TutorRuntimeStreamEvent> ExecuteStreamingAsync(
        TutorRuntimeRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(request, cancellationToken);
        yield return result.IsSuccess
            ? new TutorRuntimeStreamEvent(1, TutorRuntimeStreamEventKind.MessageCompleted,
                result.Value.ConversationId, Result: result.Value)
            : new TutorRuntimeStreamEvent(1, TutorRuntimeStreamEventKind.Error, Error: result.Error);
    }
}
