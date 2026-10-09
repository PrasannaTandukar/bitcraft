namespace Bitcraft.Domain.Benchmarking;

/// <summary>
/// Input modality a benchmark suite requires from the model under evaluation.
/// A run must not start against a model that cannot accept the suite's modality.
/// </summary>
public enum BenchmarkInputModality
{
    Text = 0,
    Image = 1
}
