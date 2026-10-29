"""One-shot generator for the frontend static task-name map (WS6, H20).

Run from the repo root with the conda python:
    .conda\\python.exe .github/plans/feature-deepeval-builtin-benchmarks/_gen_task_map.py

Single source of truth for task names is the deepeval task enums, mirrored by
sidecar/app/tools/deepeval_llm_benchmarks.py (_TASK_ENUM_NAMES). Regenerate on
any deepeval bump.
"""

from __future__ import annotations

import importlib
import json
from pathlib import Path

NAMES = {
    "big_bench_hard": "BigBenchHardTask",
    "hellaswag": "HellaSwagTask",
    "mmlu": "MMLUTask",
    "drop": "DROPTask",
    "truthful_qa": "TruthfulQATask",
    "human_eval": "HumanEvalTask",
}

HEADER = """// Static task-name map for the standardized benchmark wizard (WS6, H20).
// Generated from deepeval 4.2.x task enums; single source of truth is
// sidecar/app/tools/deepeval_llm_benchmarks.py (_TASK_ENUM_NAMES). Regenerate
// on any deepeval bump via _gen_task_map.py in the branch plan folder.

export const STANDARD_BENCHMARK_TASKS = """


def main() -> None:
    out = {
        key: [
            member.name
            for member in getattr(
                importlib.import_module(f"deepeval.benchmarks.{key}.task"), name
            )
        ]
        for key, name in NAMES.items()
    }
    target = (
        Path(__file__).resolve().parents[3]
        / "tutor-studio/src/benchmarking/standardBenchmarkTasks.js"
    )
    target.write_text(HEADER + json.dumps(out, indent=2) + ";\n", encoding="utf-8")
    print(f"wrote {target} ({sum(len(v) for v in out.values())} tasks)")


if __name__ == "__main__":
    main()
