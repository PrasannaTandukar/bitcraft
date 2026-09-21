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
    _normalize_task_scores,
    _read_overall_score,
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
