using System.Net.Sockets;
using System.Net.Http.Json;
using System.Text.Json;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bitcraft.Infrastructure.ExternalServices.Benchmarking;

public sealed class DeepEvalBenchmarkEngine(
    IHttpClientFactory httpClientFactory,
    IOptions<DeepEvalBenchmarkOptions> options,
    ILogger<DeepEvalBenchmarkEngine> logger) : IBenchmarkEngine
{
    private const string DeepEvalToolKey = "deepeval.benchmark";
    private const string SafeSidecarFailureDescription = "Benchmark sidecar evaluation failed.";
    private readonly DeepEvalBenchmarkOptions sidecarOptions = options.Value;

    public async Task<Result<BenchmarkEngineResult>> EvaluateAsync(BenchmarkEngineRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = CreateClient();
            var sidecarRequest = new SidecarEnvelope(
                DeepEvalToolKey,
                new SidecarRequest(
                    request.TemplateKey,
                    request.ConsistencyRuns,
                    request.GuidancePath,
                    request.MetricsJson,
                    request.Cases.Select(x => new SidecarCaseRequest(
                        x.CaseId,
                        x.CaseIndex,
                        x.InputText,
                        x.ActualOutput,
                        x.ExpectedOutput,
                        x.ContextText,
                        x.MetadataJson)).ToList()));

            using var httpResponse = await client.PostAsJsonAsync(
                "/internal/tools/execute",
                sidecarRequest,
                DeepEvalBenchmarkJsonContext.Default.SidecarEnvelope,
                cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var error = await httpResponse.Content.ReadFromJsonAsync(
                    DeepEvalBenchmarkJsonContext.Default.SidecarError,
                    cancellationToken);
                return Result.Failure<BenchmarkEngineResult>(new Error(
                    error?.Code ?? "Benchmark.SidecarCallFailed",
                    error?.Description ?? $"Sidecar call failed with status {(int)httpResponse.StatusCode}."));
            }

            var payload = await httpResponse.Content.ReadFromJsonAsync(
                DeepEvalBenchmarkJsonContext.Default.SidecarResponse,
                cancellationToken);

            if (payload is null)
            {
                return Result.Failure<BenchmarkEngineResult>(new Error("Benchmark.SidecarEmptyResponse", "Sidecar returned an empty response."));
            }

            var result = new BenchmarkEngineResult(
                payload.EngineName,
                payload.EngineVersion,
                new BenchmarkEngineSummary(
                    payload.Summary.AverageScore,
                    payload.Summary.PassRate,
                    payload.Summary.TotalLatencyMs,
                    payload.Summary.TotalTokens,
                    payload.Summary.TokensPerSecond,
                    payload.Summary.EstimatedCo2Grams,
                    payload.Summary.SummaryJson,
                    payload.Summary.SystemMetricsJson),
                payload.Cases.Select(x => new BenchmarkEngineCaseResult(
                    x.CaseId,
                    x.CaseIndex,
                    x.Passed,
                    x.Score,
                    x.Reason,
                    x.MetricsJson,
                    x.PerformanceJson,
                    x.ResourceJson)).ToList(),
                payload.RawResultJson);

            return Result.Success(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeepEval benchmark evaluation through sidecar failed.");
            return Result.Failure<BenchmarkEngineResult>(new Error("Benchmark.SidecarException", SafeSidecarFailureDescription));
        }
    }

    public async Task<Result<StandardLlmBenchmarkResult>> EvaluateStandardLlmBenchmarkAsync(StandardLlmBenchmarkRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = CreateClient();
            var payload = new
            {
                benchmark_key = request.BenchmarkKey,
                judge_model = new
                {
                    provider = request.Provider,
                    model_name = request.ModelName,
                    base_url = request.BaseUrl,
                    api_key_env = request.ApiKeyEnv
                },
                tasks = request.Tasks,
                n_shots = request.NShots,
                allow_code_execution = request.AllowCodeExecution
            };

            using var httpResponse = await client.PostAsJsonAsync("/internal/tools/llm-benchmark", payload, cancellationToken);
            if (!httpResponse.IsSuccessStatusCode)
            {
                return Result.Failure<StandardLlmBenchmarkResult>(new Error(
                    "Benchmark.StandardLlmFailed",
                    $"Standard LLM benchmark call failed with status {(int)httpResponse.StatusCode}."));
            }

            var responseJson = await httpResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var result = new StandardLlmBenchmarkResult(
                responseJson.TryGetProperty("benchmark_key", out var bKey) ? bKey.GetString() ?? request.BenchmarkKey : request.BenchmarkKey,
                responseJson.TryGetProperty("overall_score", out var scoreProp) && scoreProp.ValueKind == JsonValueKind.Number ? scoreProp.GetDouble() : null,
                responseJson.TryGetProperty("task_count", out var countProp) ? countProp.GetInt32() : 0,
                responseJson.TryGetProperty("engine_version", out var verProp) ? verProp.GetString() ?? "unknown" : "unknown",
                responseJson.TryGetProperty("notes", out var notesProp) ? notesProp.GetString() ?? "" : "",
                responseJson.TryGetProperty("error", out var errProp) ? errProp.GetString() : null);

            return Result.Success(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeepEval standard LLM benchmark through sidecar failed.");
            return Result.Failure<StandardLlmBenchmarkResult>(new Error("Benchmark.SidecarException", SafeSidecarFailureDescription));
        }
    }

    private HttpClient CreateClient()
    {
        var useUds = string.Equals(sidecarOptions.Transport, "uds", StringComparison.OrdinalIgnoreCase);
        if (!useUds)
        {
            var client = httpClientFactory.CreateClient(nameof(DeepEvalBenchmarkEngine));
            client.BaseAddress = new Uri(sidecarOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(sidecarOptions.TimeoutSeconds);
            return client;
        }

        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                var endpoint = new UnixDomainSocketEndPoint(sidecarOptions.SocketPath);
                await socket.ConnectAsync(endpoint, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
        };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost"),
            Timeout = TimeSpan.FromSeconds(sidecarOptions.TimeoutSeconds)
        };
    }
}
