---
created_at: 2026-05-21
edited_at: 2026-05-21
creator: planner
editor: planner
ai_gen: true
---
# Local Setup Revision — 2026-05-21

Branch: `local-setup-revision-2026-05-21` (continuation of `feature-freeze/05-tutor-document-mn`)
Lead: Lead Dev · CIL: medium · CSS: 6 · DIS: 6

## Problem Statement

The TutorRevision / dynamic-embedding / sidecar-UDS work just landed on
`feature-freeze/05-tutor-document-mn`. The local container stack now needs to
work *cohesively* across five target environments using one compose set, one
launcher surface, and one setup doc:

1. Windows + Docker Desktop (WSL2 backend)
2. macOS + Docker Desktop (Intel)
3. macOS + Docker Desktop (Apple Silicon)
4. Linux + Docker Engine (with optional NVIDIA GPU)
5. NVIDIA Jetson Orin + JetPack 6.x

Today the stack has real gaps that block any of those flows from being
"clone, run one command, working":

- Backend has no HTTP healthcheck, so `tutor-studio` races on startup.
- Jetson compose pins `dustynv/ollama:r36.2.0` (outdated), is missing the
  `ollama-pull-chat` service, has no healthcheck, and no `platform` pin, so
  amd64 hosts even attempting to read it can mis-resolve.
- GPU compose works but is under-documented; users on Linux don't know to
  install NVIDIA Container Toolkit.
- `db/Dockerfile` is orphaned drift (compose pulls `pgvector/pgvector:pg17`
  directly and never builds it).
- `tutor-studio` dev image copies source instead of bind-mounting, so there is
  no host hot-reload.
- `vite.config.js` doesn't bind `0.0.0.0` or expose a polling toggle, hurting
  WSL2 and some macOS setups.
- `run.sh` is bash-only with no Jetson menu and no Windows equivalent.
- `.env.example` is missing `OLLAMA_CHAT_MODEL`, embedding dimensions, and
  Vite polling.
- README documents modes but omits Mac, Windows, Jetson, and hot-reload caveats.

## Scope

In: backend health endpoint, compose hardening, Jetson + GPU fixes, env
template, Vite hot-reload, cross-platform launcher (Taskfile + shell mirrors),
single `docs/local-setup.md`, README cross-link, validation, inbox updates.

Out: web feature work (D25 step 9), production / CI/CD, DB migrations,
secrets, renaming `docker-compose.*.yml` to `compose.*.yaml`, the pgvector
pg17→pg18 bump (deferred — destructive to every developer's local volume),
`.gitattributes` / `.gitignore` edits.

## Decisions Made (with rationale)

- **Keep pgvector on `pgvector/pgvector:pg17`.** Bumping to `pg18-trixie` is
  cleaner long-term but invalidates every developer's local data volume. Defer
  as a separate, communicated migration (HD-1).
- **Delete `db/Dockerfile`** (HD-2). It is not wired by any compose file and
  is pure drift. Recoverable from Git history if needed.
- **Adopt Taskfile (taskfile.dev) as the primary launcher** (HD-3). Refresh
  `run.sh` to add Jetson, and add `run.ps1` as a thin PowerShell mirror.
  Taskfile is the cross-platform forward path; the two shell launchers are a
  safety net for users who don't install Task.
- **Use `extra_hosts: ["host.docker.internal:host-gateway"]`** on services
  needing host access. Safe on Win/Mac/Linux.
- **GPU compose stays on `deploy.resources.reservations.devices`.** No
  `--compatibility` flag needed. Document NVIDIA Container Toolkit prereq.
- **Jetson: upgrade to `dustynv/ollama:0.6.8-r36.4-cu126-22.04`**, add
  `ollama-pull-chat`, add curl healthcheck, add `platform: linux/arm64`,
  default `OLLAMA_CHAT_MODEL=llama3.2:1b` on that profile.
- **Backend healthcheck is a trivial minimal-API endpoint** returning 200 +
  version JSON (HD-5). Rich AspNetCore.HealthChecks deferred.
- **UDS sharing via named volume between backend and sidecar is correct as-is.**
- **No `.gitattributes` change in this plan** — needs human approval (HD-4)
  and is scheduled as a follow-up commit.

## Steps

Sequence chosen so each step is safe to land without breaking earlier ones.

| # | Step | Owner | Risk | Human-locked |
|---|------|-------|------|--------------|
| S1 | Backend `/api/health` endpoint | NET Dev | low | no |
| S2 | Compose cleanup + backend healthcheck wiring | Dev Ops | low | no |
| S3 | Jetson compose fix | Dev Ops | medium | no |
| S4 | GPU compose review | Dev Ops | low | no |
| S5 | `.env.example` completion | Lead Dev | low | no |
| S6 | Tutor Studio dev bind-mount + Vite config | Lead Dev | medium | no |
| S7 | Orphaned `db/Dockerfile` decision | Dev Ops | low | yes (HD-2) |
| S8 | Taskfile + `run.ps1` + `run.sh` refresh | Dev Ops | medium | yes (HD-3) |
| S9 | `docs/local-setup.md` | Typewriter | low | no |
| S10 | README cross-reference | Typewriter | low | no |
| S11 | Compose validation pass (6 combos) | Dev Ops | low | no |
| S12 | Inbox updates | Lead Dev | low | no |

Each step's concrete outputs, dependencies, and validation commands live in
`plan.yaml`. The narrative rationale follows:

- **S1** unlocks S2's healthcheck condition. Trivial endpoint, lowest risk
  surface in the codebase.
- **S2** is the keystone compose change: removes legacy noise, makes the
  startup graph deterministic, and lets every override file rely on
  `service_healthy`.
- **S3** is the largest correctness gap. Jetson users today have to manually
  pull the chat model and edit compose — unacceptable for a supported target.
- **S4** is mostly documentation; the GPU file already works.
- **S5** removes a class of "it ran but used the wrong model" bugs.
- **S6** fixes the dev-loop pain. Borderline frontend, but the change is
  local-dev infra (bind mounts, host binding, polling toggle) not UI
  behavior. Web Dev should sanity-check.
- **S7** removes drift. Recoverable.
- **S8** is the visible UX win for new contributors. Taskfile gives one
  command per mode on any OS; the shell mirrors cover hosts without Task.
- **S9 + S10** consolidate scattered README guidance into one
  decision-readable doc.
- **S11** is the merge gate.
- **S12** keeps the agent backlog honest.

## Human Decisions

| ID | Decision | Recommendation |
|----|----------|----------------|
| HD-1 | pgvector pg17 vs pg18 | Stay on pg17; schedule pg18 separately |
| HD-2 | Delete orphaned `db/Dockerfile`? | Delete |
| HD-3 | Adopt Taskfile? | Yes, plus keep both shell launchers |
| HD-4 | LF normalization in `.gitattributes`? | Approve in a follow-up commit (out of scope here) |
| HD-5 | `/api/health` shape | Trivial 200 + version JSON now; rich checks later |

## Risks

- **Jetson image bump (S3)** — `dustynv/ollama:0.6.8-r36.4-cu126-22.04` must
  be re-tested on a real Jetson Orin. Recommend the human owner do this
  smoke run before merging the plan branch.
- **Bind-mount on Windows (S6)** — large `node_modules` over WSL2 9p can be
  slow. The anonymous volume on `/app/node_modules` mitigates this; the
  `VITE_USE_POLLING` env handles the watcher edge cases.
- **Compose validation (S11)** is config-time only. It does not catch runtime
  pull/image-availability problems; that is what the human smoke tests in
  `validation_strategy.human_smoke` cover.
- **Taskfile adoption (S8)** assumes contributors will install Task. The
  shell mirrors are intentionally kept so the plan still works without it.

## Validation

Automated (agent-runnable):

- `dotnet build bitcraft.slnx`
- `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj`
- `dotnet test tests/Bitcraft.ApiIntegrationTests/Bitcraft.ApiIntegrationTests.csproj`
- `docker compose -f <combo> config --quiet` for all 6 documented combos
  (core, dev, dev+ai, dev+ai+gpu, dev+ai+jetson, prod)

Human smoke (Lead Dev coordinates):

- `task up:dev` on Windows (Docker Desktop / WSL2) → backend healthy +
  tutor-studio HMR working on an edit
- `task up:dev` on macOS (Apple Silicon ideally) → same
- `task up:ai-gpu` on Linux + NVIDIA → `ollama` answers on `:11434`
- `task up:ai-jetson` on Jetson Orin → both `ollama-pull-embed` and
  `ollama-pull-chat` complete; backend reaches `service_healthy`

Intentionally not run: production deploy, DB migration apply against shared
environments, live model inference benchmarks.

## Rollout

1. Land S1 first (only code-shape change in this plan).
2. Land S2–S7 in any order on top (compose-only and env-only).
3. Land S8 once S2–S7 are stable (Taskfile consumes them).
4. Land S9–S10 once shape is final.
5. Run S11 as the merge gate.
6. S12 closes the loop.

Rollback: compose changes are additive or surgical, so reverting is
`git checkout <prior commit>` plus `docker compose down && docker compose up -d`.
The new `/api/health` endpoint, Taskfile, and `run.ps1` are pure additions —
deleting them restores prior behavior. `db/Dockerfile` deletion is
recoverable from Git history.

## Out of Scope (explicit)

- D25 step 9 web stack work.
- Production deployment, CI/CD, release automation.
- Database migrations (separate dev-ops inbox).
- Secrets / JWT / identity rotation.
- Renaming `docker-compose.*.yml` → `compose.*.yaml` (style only).
- pgvector `pg17 → pg18` bump (HD-1, deferred).
- `.gitattributes` / `.gitignore` edits (HD-4, deferred).
