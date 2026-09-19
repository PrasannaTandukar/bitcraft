namespace Bitcraft.Application.Features.Benchmarking;

/// <summary>
/// Canonical registry of the standardized DeepEval benchmark suites supported by the sidecar.
/// Single source of truth for the API list endpoint and run validation; the UI renders from this.
/// Grounded against deepeval 4.2.x constructor signatures:
/// task-based benchmarks cap via n_problems_per_task, GSM8K via n_problems,
/// HumanEval accepts neither (code-execution deferred).
/// </summary>
public static class StandardBenchmarkSuiteRegistry
{
    public sealed record SuiteInfo(
        string Key,
        string DisplayName,
        string Description,
        bool SupportsTasks,
        bool SupportsShots,
        bool SupportsProblemCap,
        bool Enabled);

    public static readonly IReadOnlyList<SuiteInfo> Suites = new SuiteInfo[]
    {
        new("mmlu", "MMLU", "Massive Multitask Language Understanding: broad knowledge across 57 subjects.", SupportsTasks: true, SupportsShots: true, SupportsProblemCap: true, Enabled: true),
        new("gsm8k", "GSM8K", "Grade-school math word problems with chain-of-thought numeric answers.", SupportsTasks: false, SupportsShots: true, SupportsProblemCap: true, Enabled: true),
        new("big_bench_hard", "BIG-Bench Hard", "Challenging multi-step reasoning tasks from BIG-Bench.", SupportsTasks: true, SupportsShots: true, SupportsProblemCap: true, Enabled: true),
        new("hellaswag", "HellaSwag", "Commonsense sentence-completion prediction.", SupportsTasks: true, SupportsShots: true, SupportsProblemCap: true, Enabled: true),
        new("drop", "DROP", "Discrete reasoning over paragraphs: dates, counts, spans.", SupportsTasks: true, SupportsShots: true, SupportsProblemCap: true, Enabled: true),
        new("truthful_qa", "TruthfulQA", "Truthfulness, myth avoidance, and hallucination resistance.", SupportsTasks: true, SupportsShots: false, SupportsProblemCap: true, Enabled: true),
        // Locked decision: HumanEval requires local code execution (sandbox) and is deferred post-PoC.
        new("human_eval", "HumanEval", "Python docstring-to-code synthesis scored by unit tests. Requires a code-execution sandbox; deferred.", SupportsTasks: true, SupportsShots: false, SupportsProblemCap: false, Enabled: false),
    };

    public static SuiteInfo? Find(string key) =>
        Suites.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));

    public static bool IsEnabled(string key) => Find(key)?.Enabled == true;
}
