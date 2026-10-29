---
created_at: 2026-10-29
edited_at: 2026-10-29
creator: lead-dev
editor: lead-dev
ai_gen: true
---

# Benchmark Run Queue

This document describes the standardized benchmark run queue. It covers the
runner reliability model, the queue controls, and the Admin sidecar surface.
For tutor-scoped RAG evaluation, see `docs/benchmarking-templates.md`.

## Scope

- Suites: MMLU, GSM8K, BIG-Bench Hard, HellaSwag, DROP, TruthfulQA, and the
  custom Arduino benchmark. HumanEval stays disabled.
- Runs execute sequentially. One sidecar runner slot exists. The queue is the
  single source of scheduling truth.
- Standard runs launch from the Benchmarking page. Launch requires the Admin
  role.

## Reliability Model

The runner writes a JSON status file for every state change. Writes are
atomic. The sidecar startup sweep marks any stale non-terminal status as
failed. A stale status is one that is older than
`BENCHMARK_STALE_AFTER_SECONDS` (default 300) at sidecar start. This heals a
slot leaked by a dead runner.

Terminal writes are defensive in three layers:

1. The runner converts task scores to plain JSON objects before the write.
2. The status writer serializes Pydantic models through a `default` handler.
3. A failed terminal write falls back to a `failed` status. A completed run
   can never leave the status in `running`.

A run that cannot start stays queued. The worker retries it on each wake-up.
The run fails with `StandardBenchmark.QueueTimeout` only after it waited
longer than the standard timeout budget (default 6 hours).

A run that failed before any runner started shows a synthesized diagnostic
block in the log view. The block lists the failure code, the failure text, and
the queue time.

## Queue Controls

| Action | Applies to | Role | Endpoint |
| --- | --- | --- | --- |
| Cancel | Queued or Running runs | Admin | `POST /api/standard-benchmark-runs/{id}/cancel` |
| Requeue | Failed or Cancelled runs | Admin | `POST /api/standard-benchmark-runs/{id}/requeue` |
| Delete | Any non-running run | Admin | `DELETE /api/standard-benchmark-runs/{id}` |
| Priority | Queued runs | Admin | `PUT /api/standard-benchmark-runs/{id}/priority` |
| Archive | Any non-running run | Admin | `POST /api/standard-benchmark-runs/{id}/archive` |
| Unarchive | Archived runs | Admin | `POST /api/standard-benchmark-runs/{id}/unarchive` |
| Pause queue | Global | Admin | `PUT /api/standard-benchmark-runs/queue/paused` |
| Read queue state | Global | Auditor and above | `GET /api/standard-benchmark-runs/queue/paused` |
| Engine status | Global | Auditor and above | `GET /api/standard-benchmark-runs/engine` |
| Clear runner slot | Global | Admin | `POST /api/standard-benchmark-runs/engine/clear-slot` |

Rules:

- Cancel is terminal. A cancelled run never resumes. Use Requeue to run it
  again. Requeue clones the run.
- Archive hides a run from the default list. Archived runs are never deleted
  automatically. The Evaluations tab has an Archived switch to show them.
- Pause stops the worker from starting new runs. A running run finishes to
  completion. Resume wakes the worker immediately.
- Priority is an integer. Lower values run first.

## Admin Sidecar Surface

The Queue tab shows a Sidecar engine card. The card lists reachability, the
transport in use, the runner slot state, and the deepeval version. When the
slot is held, an Admin can press Clear slot. The action removes the persisted
slot marker. The next queued run starts immediately. The action is safe when
the slot is already free.

## Views

- **Queue tab**: queue position, per-row actions, the pause toggle, and the
  sidecar status card. Polls every 5 seconds while runs are active.
- **Run page**: live progress with completed/total counts, per-task scores,
  the runner log, and the run actions. Polls progress every 5 seconds.
- **Run log**: manual refresh plus an optional Auto switch. Auto refreshes
  every 3 seconds while the run is active. The log drops tqdm progress lines
  and can filter to error lines only.

## Interim Manual Unblock

Deployments that predate the startup sweep can clear a poisoned slot by hand.
Run this command on the sidecar container:

```powershell
docker exec bitcraft-benchmark-sidecar rm /var/run/bitcraft/runs/last_status_path.txt
docker restart bitcraft-benchmark-sidecar
```

The startup sweep removes the need for this command after this branch deploys.

## Validation

- `dotnet build bitcraft.slnx`
- `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj`
- `dotnet test tests/Bitcraft.ApiIntegrationTests/Bitcraft.ApiIntegrationTests.csproj`
- `python -m pytest tests/Bitcraft.SidecarIntegration/test_sidecar_api.py`
- `python -m pytest sidecar/tests`
- `npm run build` and `npm run lint` from `tutor-studio/`
