"""Arduino fault-detection custom benchmark (decision D-1, DR-1).

Scores a tutor model on Arduino troubleshooting: does it detect the circuit
detail, understand the circuit, reason to a solution, and teach the fix.

This iteration ships text-only. The image path is built in the base class, so
adding images later is a dataset change: add an `image` field to a record.
"""

from __future__ import annotations

from typing import List

from deepeval.metrics import BaseMetric
from deepeval.test_case import LLMTestCase

from app.tools.custom_benchmarks.base_custom_benchmark import BitcraftCustomBenchmark

# Weighted sub-scores (decision D-1). Weights sum to 1.0.
DETAIL_DETECTION_WEIGHT = 0.20
CIRCUIT_COMPREHENSION_WEIGHT = 0.30
SOLUTION_REASONING_WEIGHT = 0.30
GUIDANCE_PEDAGOGY_WEIGHT = 0.20


class _KeywordCoverageMetric(BaseMetric):
    """Scores how many expected concepts appear in the actual output.

    Deterministic and offline: no judge call. This keeps a custom benchmark run
    cheap and reproducible, and it works against a text-only model.
    """

    def __init__(
        self, name: str, keywords: List[str], weight: float, threshold: float = 0.5
    ):
        self.name = name
        self.weight = weight
        self.threshold = threshold
        self.keywords = keywords
        self.score = 0.0
        self.reason = ""
        self.success = False
        self.evaluation_model = None
        self.strict_mode = False
        self.async_mode = False
        self.include_reason = True

    def _covered(self, text: str) -> List[str]:
        lowered = (text or "").lower()
        return [word for word in self.keywords if word.lower() in lowered]

    def measure(self, test_case: LLMTestCase, *args, **kwargs) -> float:
        covered = self._covered(test_case.actual_output)
        self.score = len(covered) / len(self.keywords) if self.keywords else 0.0
        self.reason = (
            f"{self.name}: matched {len(covered)}/{len(self.keywords)} expected concepts"
            f" ({', '.join(covered) if covered else 'none'})."
        )
        self.success = self.score >= self.threshold
        return self.score

    async def a_measure(self, test_case: LLMTestCase, *args, **kwargs) -> float:
        return self.measure(test_case, *args, **kwargs)


class ArduinoBenchmark(BitcraftCustomBenchmark):
    """Arduino fault-detection benchmark. Benchmark key: `arduino`."""

    benchmark_key = "arduino"
    dataset_filename = "arduino.json"

    def build_metrics(self) -> List[BaseMetric]:
        return [
            _KeywordCoverageMetric(
                name="detail_detection",
                keywords=["pin", "resistor", "gnd", "5v"],
                weight=DETAIL_DETECTION_WEIGHT,
            ),
            _KeywordCoverageMetric(
                name="circuit_comprehension",
                keywords=["floating", "pull-up", "pullup", "current", "wire", "i2c"],
                weight=CIRCUIT_COMPREHENSION_WEIGHT,
            ),
            _KeywordCoverageMetric(
                name="solution_reasoning",
                keywords=["delay", "toggle", "input_pullup", "a4", "a5", "reconnect"],
                weight=SOLUTION_REASONING_WEIGHT,
            ),
            _KeywordCoverageMetric(
                name="guidance_pedagogy",
                keywords=["because", "should", "use", "check", "why"],
                weight=GUIDANCE_PEDAGOGY_WEIGHT,
            ),
        ]
