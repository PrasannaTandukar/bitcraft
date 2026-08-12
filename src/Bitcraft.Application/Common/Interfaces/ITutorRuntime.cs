using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Common.Interfaces;

public interface ITutorRuntime
{
    Task<Result<TutorRuntimeResult>> ExecuteAsync(
        TutorRuntimeRequest request,
        CancellationToken cancellationToken = default);
}
