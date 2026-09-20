"""Status-file helpers shared by the benchmark dispatcher and runner subprocess.

The status file is the only channel between the sidecar API process and the
runner subprocess. Writes are atomic (temp file + rename) so a reader never
observes a half-written JSON document. The status file never contains the
judge API key; the request file carries it and the runner deletes it after
reading (WS1; the key never reaches the network and never appears in logs).
"""

from __future__ import annotations

import json
import os
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def base_status(state: str, stage: str | None) -> dict:
    return {
        "state": state,
        "stage": stage,
        "overall_score": None,
        "task_scores": None,
        "task_count": 0,
        "engine_version": "",
        "notes": "",
        "error": None,
    }


def write_status(status_path: Path, payload: dict) -> None:
    """Atomically replace the status file with `payload` plus an updated_utc stamp."""
    stamped = {**payload, "updated_utc": utc_now_iso()}
    status_path = Path(status_path)
    status_path.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp_name = tempfile.mkstemp(dir=str(status_path.parent), suffix=".tmp")
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as handle:
            json.dump(stamped, handle)
        os.replace(tmp_name, status_path)
    except OSError:
        try:
            os.unlink(tmp_name)
        except OSError:
            pass
        raise


def read_status(status_path: Path) -> dict | None:
    """Return the parsed status file, or None when missing or unreadable."""
    try:
        return json.loads(Path(status_path).read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None
