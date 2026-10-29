"""WS5: custom DeepEval benchmark behaviour.

Covers dataset loading, text-only scoring, the image branch, and metric weights.
The base class constructor imports `datasets` (not installed in every local
env), so instances are built with `__new__` where the constructor is not needed.
"""

from __future__ import annotations

import importlib.util
import os
import tempfile

import pytest

# The arduino benchmark imports deepeval at module top. Skip the whole
# module when deepeval is not installed instead of failing collection
# (WS1 step 7 of the bugfix-benchmark-fixes plan).
pytest.importorskip("deepeval")

from app.tools.custom_benchmarks.arduino_benchmark import (  # noqa: E402
    CIRCUIT_COMPREHENSION_WEIGHT,
    DETAIL_DETECTION_WEIGHT,
    GUIDANCE_PEDAGOGY_WEIGHT,
    SOLUTION_REASONING_WEIGHT,
    ArduinoBenchmark,
)

_HAS_DATASETS = importlib.util.find_spec("datasets") is not None


def _instance() -> ArduinoBenchmark:
    return ArduinoBenchmark.__new__(ArduinoBenchmark)


class _StubModel:
    """Model stub that always answers with the expected diagnostic concepts."""

    def __init__(self, answer):
        self._answer = answer

    def generate(self, prompt: str) -> str:
        return self._answer

    def get_model_name(self) -> str:
        return "stub"


def test_dataset_loads_non_empty_goldens():
    goldens = _instance().load_benchmark_dataset()
    assert len(goldens) >= 1
    assert all(golden.input for golden in goldens)


def test_text_only_record_has_no_image_placeholder():
    goldens = _instance().load_benchmark_dataset()
    assert all("[DEEPEVAL:IMAGE:" not in golden.input for golden in goldens)


def test_record_with_image_embeds_placeholder():
    benchmark = _instance()
    with tempfile.TemporaryDirectory() as tmp:
        path = os.path.join(tmp, "schematic.png")
        with open(path, "wb") as handle:
            handle.write(b"\x89PNG\r\n\x1a\n")
        record = {"input": "What is wrong here?", "image": path}
        assert "[DEEPEVAL:IMAGE:" in benchmark.build_input(record)


def test_missing_image_file_falls_back_to_text():
    benchmark = _instance()
    record = {"input": "What is wrong here?", "image": "/nonexistent/nope.png"}
    assert benchmark.build_input(record) == "What is wrong here?"


def test_metric_weights_sum_to_one():
    total = (
        DETAIL_DETECTION_WEIGHT
        + CIRCUIT_COMPREHENSION_WEIGHT
        + SOLUTION_REASONING_WEIGHT
        + GUIDANCE_PEDAGOGY_WEIGHT
    )
    assert total == pytest.approx(1.0)


def test_evaluate_returns_non_zero_score_for_correct_answer():
    benchmark = _instance()
    answer = (
        "Use pinMode(2, INPUT_PULLUP) because the pin is floating. "
        "You should add a resistor to GND and check the I2C wiring."
    )
    result = benchmark.evaluate(_StubModel(answer))
    assert result.overall_accuracy > 0.0
    assert benchmark.overall_score == pytest.approx(result.overall_accuracy)


def test_evaluate_returns_zero_for_irrelevant_answer():
    benchmark = _instance()
    result = benchmark.evaluate(_StubModel("I do not know."))
    assert result.overall_accuracy == pytest.approx(0.0)


def test_evaluate_records_predictions_and_task_scores():
    benchmark = _instance()
    benchmark.evaluate(_StubModel("pin resistor gnd 5v floating pull-up delay"))
    assert len(benchmark.predictions) == len(benchmark.load_benchmark_dataset())
    assert len(benchmark.task_scores) > 0


@pytest.mark.skipif(
    not _HAS_DATASETS, reason="datasets not installed in this environment"
)
def test_constructor_builds_through_deepeval_base():
    # Full constructor path, available in the sidecar image.
    benchmark = ArduinoBenchmark()
    assert benchmark.benchmark_key == "arduino"
