using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Infrastructure.ExternalServices.Agents;

/// <summary>Runs administrator diagnostics over the same MEAI client construction used by tutors.</summary>
public sealed class RuntimeModelDiagnostic(
    IRuntimeChatClientFactory clients,
    ILogger<RuntimeModelDiagnostic> logger) : IRuntimeModelDiagnostic
{
    public async Task<Result<RuntimeModelCatalogDiagnostic>> ProbeModelsAsync(
        RuntimeModelCatalogDiagnosticRequest request,
        CancellationToken cancellationToken = default)
    {
        var model = new TutorAgentModelConfiguration(Guid.Empty, request.ProviderType, request.Endpoint,
            request.ApiKey, Guid.Empty, "catalog-probe", 0);
        var result = await clients.ProbeAvailableModelsAsync(model, cancellationToken);
        if (result.IsFailure)
        {
            logger.LogWarning("Runtime model catalog probe failed for provider {ProviderType} with category {ErrorCode}",
                request.ProviderType, result.Error.Code);
            return Result.Failure<RuntimeModelCatalogDiagnostic>(result.Error);
        }
        return Result.Success(new RuntimeModelCatalogDiagnostic(result.Value));
    }

    public async Task<Result<RuntimeModelGenerationDiagnostic>> GenerateAsync(
        RuntimeModelGenerationDiagnosticRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = clients.Create(new TutorAgentModelConfiguration(request.RuntimeEngineId,
            request.ProviderType, request.Endpoint, request.ApiKey, request.ModelId,
            request.ProviderModelName, request.Capabilities));
        if (created.IsFailure)
            return Result.Failure<RuntimeModelGenerationDiagnostic>(created.Error);

        using var client = created.Value;
        var message = new ChatMessage(ChatRole.User, request.Prompt);
        foreach (var image in request.Images ?? [])
            message.Contents.Add(new DataContent(image.Data, image.MimeType));
        try
        {
            var response = await client.GetResponseAsync([message], new ChatOptions
            {
                MaxOutputTokens = request.MaxOutputTokens
            }, cancellationToken);
            if (string.IsNullOrWhiteSpace(response.Text))
                return Result.Failure<RuntimeModelGenerationDiagnostic>(new Error(
                    "RuntimeDiagnostic.EmptyResponse", "The configured model returned an empty response."));
            return Result.Success(new RuntimeModelGenerationDiagnostic(response.Text.Trim()));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Runtime model generation diagnostic failed. ExceptionType={ExceptionType}, Provider={ProviderType}, RuntimeEngineId={RuntimeEngineId}, ModelId={ModelId}",
                ex.GetType().FullName, request.ProviderType, request.RuntimeEngineId, request.ModelId);
            return Result.Failure<RuntimeModelGenerationDiagnostic>(new Error(
                "RuntimeDiagnostic.GenerationFailed", "The configured model diagnostic failed."));
        }
    }
}
