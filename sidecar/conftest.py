"""Pytest bootstrap: make the sidecar `app` package importable from tests/."""

from __future__ import annotations

import sys
from pathlib import Path

_SIDECAR_ROOT = Path(__file__).resolve().parent
if str(_SIDECAR_ROOT) not in sys.path:
    sys.path.insert(0, str(_SIDECAR_ROOT))
