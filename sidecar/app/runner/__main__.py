"""Benchmark runner subprocess entry point.

Usage: python -m app.runner --request-file <path> --status-file <path>

One subprocess executes exactly one standardized benchmark run (plan
standard-benchmark-hardening WS1, decision D-R1). The synchronous
deepeval evaluate() call runs here, off the sidecar's uvicorn event loop,
so /health stays responsive while a benchmark is in progress.

The runner writes a JSON status file at every state change:
  {"state": "running"|"completed"|"failed", "stage": str,
   "overall_score": float|null, "task_scores": [{"task": str, "score": float}]|null,
   "task_count": int, "engine_version": str, "notes": str, "error": str|null,
   "updated_utc": str}

The request file carries the full SidecarLlmBenchmarkRequest payload
including the judge API key. The runner deletes the request file after
reading it so the key does not linger on the shared volume.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

from app.runner.status import base_status, write_status
from app.schemas import SidecarLlmBenchmarkRequest
from app.tools.deepeval_llm_benchmarks import execute_benchmark


def _parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run one standardized benchmark.")
    parser.add_argument(
        "--request-file", required=True, help="Path to the JSON request payload."
    )
    parser.add_argument(
        "--status-file", required=True, help="Path to the JSON status file to write."
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = _parse_args(argv)
    request_path = Path(args.request_file)
    status_path = Path(args.status_file)

    try:
        raw = json.loads(request_path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        write_status(
            status_path,
            {
                **base_status("failed", None),
                "error": f"Runner could not read the request file: {error}",
            },
        )
        return 2
    finally:
        # The request file carries the judge API key. Delete it immediately
        # after reading so the key does not linger on the shared volume.
        try:
            request_path.unlink()
        except OSError:
            pass

    try:
        request = SidecarLlmBenchmarkRequest.model_validate(raw)
    except ValueError as error:
        write_status(
            status_path,
            {
                **base_status("failed", None),
                "error": f"Runner request validation failed: {error}",
            },
        )
        return 2

    write_status(status_path, {**base_status("running", "loading-dataset")})
    result = execute_benchmark(
        request,
        stage_writer=lambda stage, progress=None: write_status(
            status_path, {**base_status("running", stage), **(progress or {})}
        ),
    )

    if result.error:
        write_status(
            status_path,
            {
                **base_status("failed", None),
                "engine_version": result.engine_version,
                "error": result.error,
            },
        )
        return 1

    write_status(
        status_path,
        {
            **base_status("completed", "finalizing"),
            "overall_score": result.overall_score,
            "task_scores": result.task_scores,
            "task_count": result.task_count,
            "completed_tasks": result.completed_tasks,
            "total_tasks": result.total_tasks,
            "engine_version": result.engine_version,
            "notes": result.notes,
        },
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
