"""WS1: non-zero score path and task-score normalization for the sidecar.

These tests exercise the pure helpers that read a deepeval benchmark result,
using stub objects shaped like the installed deepeval 4.2.3 concrete classes
(`benchmark.overall_score` float, `task_scores` pandas DataFrame with Task/Score
columns). They do not call a real model.

The DataFrame branch is tested with a duck-typed stub (``iterrows`` + ``columns``)
so the suite runs in environments where pandas is not installed (the sidecar
runs in Docker with its own requirements). A real-pandas test is included and
skipped when pandas is unavailable.
"""

from __future__ import annotations

import importlib.util

import pytest

from app.schemas import SidecarTaskScore
from app.tools.deepeval_llm_benchmarks import (
    _ProgressTracker,
    _cap_log,
    _effective_task_count,
    _estimate_total_tasks,
    _normalize_task_scores,
    _read_overall_score,
    _scrub_secrets,
    read_run_log,
)

_HAS_PANDAS = importlib.util.find_spec("pandas") is not None


class _StubResult:
    def __init__(self, overall_accuracy=None, overall_score=None):
        self.overall_accuracy = overall_accuracy
        self.overall_score = overall_score


class _StubBenchmark:
    def __init__(self, overall_score=None, task_scores=None):
        self.overall_score = overall_score
        self.task_scores = task_scores


class _DataFrameLike:
    """Duck-typed stand-in for a pandas DataFrame with Task/Score columns."""

    def __init__(self, rows):
        self._rows = rows
        self.columns = ["Task", "Score"]

    def iterrows(self):
        for index, (task, score) in enumerate(self._rows):
            yield index, {"Task": task, "Score": score}


def test_read_overall_score_prefers_instance_attribute():
    benchmark = _StubBenchmark(overall_score=0.85)
    result = _StubResult(overall_accuracy=0.10)
    assert _read_overall_score(benchmark, result) == pytest.approx(0.85)


def test_read_overall_score_falls_back_to_result_accuracy():
    benchmark = _StubBenchmark(overall_score=None)
    result = _StubResult(overall_accuracy=0.62)
    assert _read_overall_score(benchmark, result) == pytest.approx(0.62)


def test_read_overall_score_returns_zero_when_absent():
    benchmark = _StubBenchmark(overall_score=None)
    result = _StubResult(overall_accuracy=None)
    assert _read_overall_score(benchmark, result) == 0.0


def test_normalize_task_scores_from_dataframe_like():
    raw = _DataFrameLike([("abstract_algebra", 0.75), ("anatomy", 0.25)])
    scores = _normalize_task_scores(raw)
    assert scores == [
        SidecarTaskScore(task="abstract_algebra", score=0.75),
        SidecarTaskScore(task="anatomy", score=0.25),
    ]


def test_normalize_task_scores_from_dict():
    scores = _normalize_task_scores({"a": 0.5, "b": 1.0})
    assert scores == [
        SidecarTaskScore(task="a", score=0.5),
        SidecarTaskScore(task="b", score=1.0),
    ]


def test_normalize_task_scores_returns_none_when_empty():
    assert _normalize_task_scores(None) is None
    assert _normalize_task_scores(_DataFrameLike([])) is None


@pytest.mark.skipif(not _HAS_PANDAS, reason="pandas not installed in this environment")
def test_normalize_task_scores_from_real_pandas_dataframe():
    import pandas as pd

    df = pd.DataFrame({"Task": ["abstract_algebra", "anatomy"], "Score": [0.75, 0.25]})
    scores = _normalize_task_scores(df)
    assert scores == [
        SidecarTaskScore(task="abstract_algebra", score=0.75),
        SidecarTaskScore(task="anatomy", score=0.25),
    ]


# ----- Bug A: total task count must come from the benchmark, not the request -----


class _TaskedBenchmark:
    """Stub shaped like a deepeval benchmark carrying a `tasks` list."""

    def __init__(self, tasks):
        self.tasks = tasks


def test_effective_task_count_reads_benchmark_tasks_when_request_is_null():
    # Regression: a null `tasks` request runs the suite's full task enum, so the
    # count must come from the benchmark. Requesting 1 would report 1 instead of 57.
    benchmark = _TaskedBenchmark(list(range(57)))
    assert _effective_task_count(benchmark, None) == 57


def test_effective_task_count_falls_back_to_resolved_tasks():
    benchmark = _TaskedBenchmark(None)
    assert _effective_task_count(benchmark, ["a", "b"]) == 2


def test_effective_task_count_returns_one_when_nothing_is_known():
    benchmark = _TaskedBenchmark(None)
    assert _effective_task_count(benchmark, None) == 1


def test_estimate_total_tasks_multiplies_by_problem_cap():
    benchmark = _TaskedBenchmark(list(range(3)))
    assert _estimate_total_tasks(benchmark, None, 2) == 6


def test_estimate_total_tasks_returns_none_without_a_cap():
    # An uncapped run cannot know the dataset size before evaluation finishes.
    benchmark = _TaskedBenchmark(list(range(3)))
    assert _estimate_total_tasks(benchmark, None, 0) is None


# ----- DR-3: throttled per-prediction progress -----


def test_progress_tracker_counts_every_prediction():
    writes = []
    tracker = _ProgressTracker(
        lambda stage, progress: writes.append((stage, progress)), 10
    )
    for _ in range(5):
        tracker.tick()
    assert tracker.count == 5


def test_progress_tracker_throttles_writes():
    writes = []
    tracker = _ProgressTracker(
        lambda stage, progress: writes.append((stage, progress)), 10, interval=1.0
    )
    for _ in range(50):
        tracker.tick()
    # One second has not elapsed, so the 50 ticks collapse to a single write.
    assert len(writes) == 1


def test_progress_tracker_flush_writes_final_count():
    writes = []
    tracker = _ProgressTracker(
        lambda stage, progress: writes.append((stage, progress)), 10, interval=1.0
    )
    for _ in range(7):
        tracker.tick()
    tracker.flush()
    assert writes[-1] == ("evaluating", {"completed_tasks": 7, "total_tasks": 10})


def test_progress_tracker_without_writer_is_safe():
    tracker = _ProgressTracker(None, 10)
    tracker.tick()
    tracker.flush()
    assert tracker.count == 1


# ----- DR-6: log capture, capping, and secret scrubbing -----


def test_scrub_secrets_removes_the_key():
    text = "Authorization: Bearer sk-super-secret-value"
    assert _scrub_secrets(text, "sk-super-secret-value") == (
        "Authorization: Bearer [REDACTED]"
    )


def test_scrub_secrets_ignores_short_values():
    # A short value is likely a coincidental substring; scrubbing it would
    # corrupt unrelated log text.
    assert _scrub_secrets("abc short abc", "short") == "abc short abc"


def test_scrub_secrets_without_a_key_returns_text_unchanged():
    assert _scrub_secrets("plain line", None) == "plain line"


def test_cap_log_truncates_oversized_file(tmp_path):
    log = tmp_path / "run.log"
    log.write_text("x" * (3 * 1024 * 1024), encoding="utf-8")
    _cap_log(log)
    assert log.stat().st_size < 2 * 1024 * 1024
    assert "[log truncated]" in log.read_text(encoding="utf-8")


def test_cap_log_leaves_small_file_untouched(tmp_path):
    log = tmp_path / "run.log"
    log.write_text("small", encoding="utf-8")
    _cap_log(log)
    assert log.read_text(encoding="utf-8") == "small"


def test_cap_log_missing_file_is_safe(tmp_path):
    _cap_log(tmp_path / "absent.log")


def test_read_run_log_rejects_traversal():
    assert read_run_log("../etc/passwd") is None
    assert read_run_log("a/b") is None
