using Bitcraft.Application.Common.Models;
using Microsoft.Extensions.AI;

namespace Bitcraft.Infrastructure.ExternalServices.Agents;

public interface IRuntimeChatClientFactory
{
    /// <summary>The caller owns and must dispose the returned client after the turn.</summary>
    Result<IChatClient> Create(TutorAgentModelConfiguration model);

    Task<Result<IReadOnlyList<string>>> ProbeAvailableModelsAsync(
        TutorAgentModelConfiguration model,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure<IReadOnlyList<string>>(new Error(
            "RuntimeDiagnostic.CatalogUnsupported",
            "This runtime provider does not expose a supported model catalog.")));
}
