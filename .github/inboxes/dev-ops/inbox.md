---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# Dev Ops Inbox

- Date: 2026-05-19
- Status: active
- Source: Human (D-3)
- Target: dev-ops
- Priority: medium
- Task: Adopt the branch convention captured in `.github/governance/BRANCH_CONVENTION.md`: plan folder slug is derived from the working Git branch; `dev` / `main` / `test` are protected (agents HALT and notify the human). Confirm CI branch filters and merge policy are compatible, and document any deviations.
- Context: Supersedes the original "propose a branch convention" item. Agents do not perform Git writes. Coordinate with `.github/plans/` folder naming and the existing `batch-2-agentic-stack/` placeholder.
- Acceptance/Cleanup: CI and merge policy compatibility confirmed (or follow-up items raised); remove this item.

- Date: 2026-05-19
- Status: active
- Source: Planner
- Target: dev-ops
- Priority: low
- Task: Confirm the `.github/plans/` folder design is compatible with the adopted branch convention. Flag any naming collisions in active folders.
- Context: Branch convention now lives in `.github/governance/BRANCH_CONVENTION.md`.
- Acceptance/Cleanup: Confirm or suggest adjustments; remove this item.

- Date: 2026-05-18
- Status: parked
- Source: AI Ops
- Target: dev-ops
- Priority: medium
- Task: Schedule the first Integrity Audit after the branch convention direction is approved.
- Context: Use `.github/audits/templates/audit.template.md` and create remediation inbox items rather than broad report sprawl.
- Acceptance/Cleanup: Audit is completed or converted into concrete remediation tasks; remove this item afterward.

- Date: 2026-05-20
- Status: active
- Source: AI Ops (feature-freeze v3.1 seeding)
- Target: dev-ops
- Priority: high
- Task: Implement env-vars-first configuration per D9 and twelve-factor III. Move runtime configuration out of `appsettings.json` into environment variables for production-bound deployments; keep `template.<file>.<ext>` reserved for cases where a sample is genuinely required (not as a routine bootstrap). Gate CI on the absence of accidentally committed secrets or production-only `appsettings.*.json` values.
- Context: feature-freeze-2026-05-21 WS2, D9, **D26** (`BITCRAFT_RUNTIME_LOCAL_ONLY` conditional compilation symbol defined in repo-root `Directory.Build.props` under `Configuration == 'Local'` guard; off in `Debug`/`Release`; CI never sets it); [governance/twelve-factor.md](../../governance/twelve-factor.md) Factor III + Factor V.
- Acceptance/Cleanup: Compose files and Dockerfiles read from env; CI check passes; remove this item.

- Date: 2026-05-20
- Status: active
- Source: AI Ops (feature-freeze v3.1 seeding)
- Target: dev-ops
- Priority: medium
- Task: Add a deployment-level ANN strategy setting (HNSW default, IVFFlat alternative per D5.a) and wire it through compose/env so `EnableEmbeddingModelCommand` / `DisableEmbeddingModelCommand` can read it at command-execution time. Document the switch procedure (re-enable the embedding model after a strategy change).
- Context: feature-freeze-2026-05-21 WS5, D5, D5.a.
- Acceptance/Cleanup: Setting documented in compose; NET Dev confirms the command path reads it; remove this item.

- Date: 2026-05-20
- Status: active
- Source: AI Ops (feature-freeze v3.1 seeding)
- Target: dev-ops
- Priority: low
- Task: Begin tracking twelve-factor Partial/Gap items in [governance/twelve-factor.md](../../governance/twelve-factor.md). Open follow-up inbox items for Factors V (release-stage config injection), IX (vectorization idempotency), and XI (structured logging contract) when the post-freeze window opens.
- Context: [HUMAN_DECISIONS.md D-9](../../governance/HUMAN_DECISIONS.md), [twelve-factor.md](../../governance/twelve-factor.md).
- Acceptance/Cleanup: Post-freeze trigger reached and follow-up items opened; remove this item.
