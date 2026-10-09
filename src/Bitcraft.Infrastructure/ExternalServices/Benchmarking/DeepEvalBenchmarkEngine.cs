using System.Net.Sockets;
using System.Net.Http.Json;
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

    // One cached UDS client for the engine lifetime. MS Learn advises reusing
    // HttpClient instances because per-call construction risks socket
    // exhaustion. The HTTP fallback keeps the IHttpClientFactory-managed client.
    private readonly Lazy<HttpClient> udsClient = new(() => CreateUdsClient(options.Value));

    public async Task<Result<BenchmarkEngineResult>> EvaluateAsync(BenchmarkEngineRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var lease = ResolveClient();
            var client = lease.Client;
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

    public async Task<Result<StandardBenchmarkHandle>> StartStandardLlmBenchmarkAsync(StandardLlmBenchmarkRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var lease = ResolveClient();
            var client = lease.Client;
            var payload = new SidecarStandardBenchmarkStartRequest(
                request.BenchmarkKey,
                new SidecarJudgeModelPayload(request.Provider, request.ModelName, request.BaseUrl, request.ApiKey),
                request.Tasks,
                request.NShots,
                request.NProblems,
                request.AllowCodeExecution);

            using var startResponse = await client.PostAsJsonAsync(
                "/internal/tools/llm-benchmark",
                payload,
                DeepEvalBenchmarkJsonContext.Default.SidecarStandardBenchmarkStartRequest,
                cancellationToken);

            if ((int)startResponse.StatusCode == 409)
            {
                return Result.Failure<StandardBenchmarkHandle>(new Error(
                    "StandardBenchmark.SidecarBusy",
                    "The benchmark sidecar is already running a standardized benchmark."));
            }

            if (!startResponse.IsSuccessStatusCode)
            {
                var error = await startResponse.Content.ReadFromJsonAsync(
                    DeepEvalBenchmarkJsonContext.Default.SidecarError,
                    cancellationToken);
                return Result.Failure<StandardBenchmarkHandle>(new Error(
                    error?.Code ?? "Benchmark.StandardLlmFailed",
                    error?.Description ?? $"Standard LLM benchmark start failed with status {(int)startResponse.StatusCode}."));
            }

            var started = await startResponse.Content.ReadFromJsonAsync(
                DeepEvalBenchmarkJsonContext.Default.SidecarStandardBenchmarkStartResponse,
                cancellationToken);
            if (started is null || string.IsNullOrWhiteSpace(started.RunId))
            {
                return Result.Failure<StandardBenchmarkHandle>(new Error("Benchmark.SidecarEmptyResponse", "Sidecar returned an empty start response."));
            }

            return Result.Success(new StandardBenchmarkHandle(started.RunId));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Starting DeepEval standard LLM benchmark through sidecar failed.");
            return Result.Failure<StandardBenchmarkHandle>(new Error("Benchmark.SidecarException", SafeSidecarFailureDescription));
        }
    }

    public async Task<Result<bool>> CancelStandardLlmBenchmarkAsync(StandardBenchmarkHandle handle, CancellationToken cancellationToken = default)
    {
        try
        {
            using var lease = ResolveClient();
            var client = lease.Client;
            using var response = await client.PostAsync($"/internal/benchmarks/{Uri.EscapeDataString(handle.RunId)}/cancel", null, cancellationToken);

            // 404 means no live runner is held. The caller still marks the run
            // cancelled, so an already-finished run is not an error here.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return Result.Success(false);
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync(
                    DeepEvalBenchmarkJsonContext.Default.SidecarError,
                    cancellationToken);
                return Result.Failure<bool>(new Error(
                    error?.Code ?? "Benchmark.StandardLlmCancelFailed",
                    error?.Description ?? $"Standard LLM benchmark cancel failed with status {(int)response.StatusCode}."));
            }

            return Result.Success(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cancelling DeepEval standard LLM benchmark through sidecar failed.");
            return Result.Failure<bool>(new Error("Benchmark.SidecarException", SafeSidecarFailureDescription));
        }
    }

    public async Task<Result<string?>> GetStandardLlmBenchmarkLogAsync(StandardBenchmarkHandle handle, CancellationToken cancellationToken = default)
    {
        try
        {
            using var lease = ResolveClient();
            var client = lease.Client;
            using var response = await client.GetAsync($"/internal/benchmarks/{Uri.EscapeDataString(handle.RunId)}/log", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return Result.Success<string?>(null);
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync(
                    DeepEvalBenchmarkJsonContext.Default.SidecarError,
                    cancellationToken);
                return Result.Failure<string?>(new Error(
                    error?.Code ?? "Benchmark.StandardLlmLogFailed",
                    error?.Description ?? $"Standard LLM benchmark log call failed with status {(int)response.StatusCode}."));
            }

            var payload = await response.Content.ReadFromJsonAsync(
                DeepEvalBenchmarkJsonContext.Default.SidecarStandardBenchmarkLogResponse,
                cancellationToken);
            return Result.Success<string?>(payload?.Log);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reading DeepEval standard LLM benchmark log through sidecar failed.");
            return Result.Failure<string?>(new Error("Benchmark.SidecarException", SafeSidecarFailureDescription));
        }
    }

    public async Task<Result<StandardBenchmarkStatus>> GetStandardLlmBenchmarkStatusAsync(StandardBenchmarkHandle handle, CancellationToken cancellationToken = default)
    {
        try
        {
            using var lease = ResolveClient();
            var client = lease.Client;
            using var statusResponse = await client.GetAsync($"/internal/benchmarks/{Uri.EscapeDataString(handle.RunId)}/status", cancellationToken);

            if (!statusResponse.IsSuccessStatusCode)
            {
                var error = await statusResponse.Content.ReadFromJsonAsync(
                    DeepEvalBenchmarkJsonContext.Default.SidecarError,
                    cancellationToken);
                return Result.Failure<StandardBenchmarkStatus>(new Error(
                    error?.Code ?? "Benchmark.StandardLlmStatusFailed",
                    error?.Description ?? $"Standard LLM benchmark status call failed with status {(int)statusResponse.StatusCode}."));
            }

            var status = await statusResponse.Content.ReadFromJsonAsync(
                DeepEvalBenchmarkJsonContext.Default.SidecarStandardBenchmarkStatusResponse,
                cancellationToken);
            if (status is null)
            {
                return Result.Failure<StandardBenchmarkStatus>(new Error("Benchmark.SidecarEmptyResponse", "Sidecar returned an empty status response."));
            }

            return Result.Success(new StandardBenchmarkStatus(
                status.State,
                status.Stage,
                status.OverallScore,
                status.TaskScores?.Select(x => new StandardBenchmarkTaskScore(x.Task, x.Score)).ToList(),
                status.TaskCount,
                status.CompletedTasks,
                status.TotalTasks,
                status.EngineVersion,
                status.Notes,
                status.Error));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reading DeepEval standard LLM benchmark status through sidecar failed.");
            return Result.Failure<StandardBenchmarkStatus>(new Error("Benchmark.SidecarException", SafeSidecarFailureDescription));
        }
    }

    private ClientLease ResolveClient()
    {
        var useUds = string.Equals(sidecarOptions.Transport, "uds", StringComparison.OrdinalIgnoreCase);
        return useUds
            ? new ClientLease(udsClient.Value, Owns: false)
            : new ClientLease(httpClientFactory.CreateClient(nameof(DeepEvalBenchmarkEngine)), Owns: true);
    }

    /// <summary>Disposes the client only when the engine created it (factory path).
    /// The cached UDS client lives for the engine lifetime.</summary>
    private readonly record struct ClientLease(HttpClient Client, bool Owns) : IDisposable
    {
        public void Dispose()
        {
            if (Owns)
            {
                Client.Dispose();
            }
        }
    }

    private static HttpClient CreateUdsClient(DeepEvalBenchmarkOptions sidecarOptions)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                var endpoint = new UnixDomainSocketEndPoint(sidecarOptions.SocketPath);
                await socket.ConnectAsync(endpoint, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            },
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost"),
            // Short per-call timeout only. Long benchmark runs are governed by
            // the Application poll loop and its cancellation token, never by a
            // multi-hour HttpClient.Timeout (MS Learn: only the shorter of the
            // two timeouts applies).
            Timeout = TimeSpan.FromSeconds(sidecarOptions.TimeoutSeconds)
        };
    }
}
