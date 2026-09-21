"""Shared base class for Bitcraft custom DeepEval benchmarks.

Subclasses own only their dataset file and metric set. This base owns dataset
loading from an immutable JSON file, Golden mapping (including the optional
image branch), and the evaluate skeleton that produces a
`DeepEvalBaseBenchmarkResult`.

Image support (decision D-1 / DR-1): a dataset record may carry an `image`
field. When present, the record's input is built as a multimodal prompt with an
embedded `[DEEPEVAL:IMAGE:<id>]` placeholder. Text-only records leave it null,
so enabling images later is a data change, not a code change.
"""

from __future__ import annotations

import json
from abc import abstractmethod
from pathlib import Path
from typing import List

from deepeval.benchmarks.base_benchmark import (
    DeepEvalBaseBenchmark,
    DeepEvalBaseBenchmarkResult,
)
from deepeval.dataset import Golden
from deepeval.metrics import BaseMetric
from deepeval.models.base_model import DeepEvalBaseLLM
from deepeval.test_case import LLMTestCase, MLLMImage


class BitcraftCustomBenchmark(DeepEvalBaseBenchmark):
    """Base class for a shipped, read-only custom benchmark.

    Subclasses set `benchmark_key` and `dataset_filename`, and implement
    `build_metrics`. The dataset lives next to the subclass module in a
    `datasets/` directory inside the sidecar image.
    """

    benchmark_key: str = ""
    dataset_filename: str = ""

    def __init__(self, dataset=None):
        super().__init__(dataset)
        self.predictions = None
        self.task_scores = None
        self.overall_score: float | None = None

    # ----- Dataset -----

    @property
    def dataset_path(self) -> Path:
        module_dir = Path(__file__).resolve().parent
        return module_dir / "datasets" / self.dataset_filename

    def load_benchmark_dataset(self, *args, **kwargs) -> List[Golden]:
        """Load the shipped JSON dataset into Goldens."""
        records = json.loads(self.dataset_path.read_text(encoding="utf-8"))
        return [self.case_to_golden(record) for record in records]

    def case_to_golden(self, record: dict) -> Golden:
        """Map one dataset record to a Golden.

        A record without an `image` field produces a text-only Golden. A record
        with an `image` field produces a Golden whose input carries an embedded
        image placeholder (DR-1).
        """
        return Golden(
            input=self.build_input(record),
            expected_output=record.get("expected_output"),
            context=record.get("context") or None,
            additional_metadata=record.get("metadata"),
        )

    def build_input(self, record: dict) -> str:
        """Build the prompt for one record, embedding an image when present."""
        prompt = record.get("input", "")
        image_path = record.get("image")
        if not image_path:
            return prompt

        placeholder = self.embed_image(image_path)
        if placeholder is None:
            return prompt
        return f"{prompt}\n{placeholder}"

    def embed_image(self, image_path: str) -> str | None:
        """Build an `[DEEPEVAL:IMAGE:<id>]` placeholder for a local image.

        deepeval 4.2.3's MLLMImage takes the path in `url` and uses `local` as a
        boolean flag (verified at llm_test_case.py:41-80). Constructing the
        MLLMImage registers it in the module-level registry, so the placeholder
        can be embedded in the input string.
        """
        try:
            image = MLLMImage(url=image_path, local=True)
        except Exception:  # noqa: BLE001 - a bad image path must not fail the load.
            return None
        return f"[DEEPEVAL:IMAGE:{image._id}]"

    # ----- Metrics -----

    @abstractmethod
    def build_metrics(self) -> List[BaseMetric]:
        """Return the metrics this benchmark scores with."""
        raise NotImplementedError

    # ----- Evaluate -----

    def evaluate(
        self, model: DeepEvalBaseLLM, *args, **kwargs
    ) -> DeepEvalBaseBenchmarkResult:
        goldens = self.load_benchmark_dataset()
        metrics = self.build_metrics()

        total_weight = sum(self.weight_for(metric) for metric in metrics) or 1.0
        weighted_total = 0.0
        rows = []
        scores_by_task = []

        for golden in goldens:
            actual_output = self.generate(model, golden)
            test_case = LLMTestCase(
                input=golden.input,
                actual_output=actual_output,
                expected_output=golden.expected_output,
                context=golden.context,
                multimodal=self.is_multimodal(golden),
            )

            case_weighted = 0.0
            for metric in metrics:
                metric.measure(test_case)
                score = metric.score or 0.0
                weight = self.weight_for(metric)
                case_weighted += score * weight
                scores_by_task.append((self.metric_name(metric), score))

            case_score = case_weighted / total_weight
            weighted_total += case_score
            rows.append((golden.input, actual_output, case_score))

        overall_accuracy = weighted_total / len(goldens) if goldens else 0.0
        self.predictions = rows
        self.task_scores = scores_by_task
        self.overall_score = overall_accuracy
        return DeepEvalBaseBenchmarkResult(overall_accuracy=overall_accuracy)

    def generate(self, model: DeepEvalBaseLLM, golden: Golden) -> str:
        """Generate a model answer for one Golden."""
        return model.generate(golden.input)

    @staticmethod
    def is_multimodal(golden: Golden) -> bool:
        return "[DEEPEVAL:IMAGE:" in (golden.input or "")

    @staticmethod
    def weight_for(metric: BaseMetric) -> float:
        return float(getattr(metric, "weight", 1.0))

    @staticmethod
    def metric_name(metric: BaseMetric) -> str:
        return getattr(metric, "name", metric.__class__.__name__)
