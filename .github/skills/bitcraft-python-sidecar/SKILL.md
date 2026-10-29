---
created_at: 2026-09-21
edited_at: 2026-09-21
creator: ai-ops
editor: ai-ops
ai_gen: true
name: bitcraft-python-sidecar
description: "Implement or change the Bitcraft Python FastAPI sidecar in sidecar/: benchmark runner, tool registry, schemas, and transport (UDS default, optional HTTP). Use for sidecar endpoints, deepeval benchmark execution, dataset handling, and sidecar tests."
argument-hint: "[sidecar endpoint, runner, tool, or test change]"
---

# Bitcraft Python Sidecar

Use this skill for work in `sidecar/`. Read `sidecar/README.md` and the nearest existing module before editing.

## Procedure

1. Locate the owning module: `app/main.py` (routes), `app/schemas.py` (Pydantic models), `app/tool_registry.py`, or `app/runner/`.
2. Keep the transport contract stable: UDS at `/var/run/bitcraft/sidecar.sock` is the default. HTTP on port 48317 is optional.
3. Validate inputs before starting work. Fail fast with 400 on invalid requests. Do not mask validation errors behind busy states.
4. Write status files atomically: write to a temp file, then rename. Delete key-bearing request files in a `finally` block.
5. Guard all path construction against traversal. Reject `/` and `..` segments.
6. Keep dependencies pinned in `sidecar/requirements.txt`. Add a dependency only when the plan names it.
7. Run the sidecar test suite: `python -m pytest tests/Bitcraft.SidecarIntegration/test_sidecar_api.py` from the repository root.

## Constraints

- Do not send API keys over the network. Keys travel only over the local UDS and never appear in logs.
- Do not enable telemetry. Keep `DEEPEVAL_TELEMETRY_OPT_OUT=1`.
- Do not touch `src/Bitcraft.*` C# projects. If the C# engine contract must change, route to `bitcraft-backend-feature`.

## Output

- Changed modules and endpoints.
- Test run result.
- Next step: "Run the `bitcraft-review` skill for branch `<branch>`. Scope: <changed files>."
