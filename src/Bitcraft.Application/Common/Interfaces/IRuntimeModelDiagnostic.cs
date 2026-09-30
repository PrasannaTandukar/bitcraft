using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Common.Interfaces;

/// <summary>
/// Framework-neutral boundary for administrator connectivity and model diagnostics.
/// Tutor execution must use <see cref="ITutorAgentExecutor"/> instead.
/// </summary>
public interface IRuntimeModelDiagnostic
{
    Task<Result<RuntimeModelCatalogDiagnostic>> ProbeModelsAsync(
        RuntimeModelCatalogDiagnosticRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RuntimeModelGenerationDiagnostic>> GenerateAsync(
        RuntimeModelGenerationDiagnosticRequest request,
        CancellationToken cancellationToken = default);
}
