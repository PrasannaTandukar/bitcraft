---
created_at: 2026-05-19
edited_at: 2026-05-21
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# Dev Ops Inbox

- Date: 2026-05-21
- Status: pending Human action after PR #4-8 merge
- Source: Lead Dev (plan v3.4 / D25 steps 4-8)
- Target: dev-ops
- Priority: high (release-blocking for any env running merged step 4-8 code)
- Task: Apply EF migration `20260520190952_Documents_Folders_DynamicDims_SnapshotVersions_20260521`. Migration drops the HNSW index on `KnowledgeChunks.Embedding`, relaxes the column from `vector(768)` to untyped `vector`, adds `EmbeddingModelId` + `EmbeddingDimensions`, creates `DocumentFolders`, adds `Documents.FolderId`, and adds `TutorConfigurationSnapshots.VersionNumber` (with backfill SQL ordered by CapturedUtc/Id before the unique index). `Down()` re-creates the HNSW index but rejects any rows whose embedding dimension differs from 768.
- Command: `dotnet ef database update --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api`.
- Notes: After apply, monitor `/api/knowledge/...` similarity query latency — HNSW is gone, so cosine search is sequential. Acceptable for current data volume; per-model partitioned ANN indices are a deferred follow-up. Verify the snapshot backfill set unique versions for every tutor: `SELECT "TutorId", COUNT(*) FROM "TutorConfigurationSnapshots" GROUP BY "TutorId" HAVING COUNT(DISTINCT "VersionNumber") <> COUNT(*);` must return zero rows.
- Acceptance/Cleanup: Close after migration applied in dev + staging + prod and the verification query is empty.

- Date: 2026-05-21
- Status: pending Human action after PR #2 merge
- Source: Lead Dev (plan v3.4 / D25 step 2)
- Target: dev-ops
- Priority: high (release-blocking for any env that runs the merged step 2 code)
- Task: Apply EF migration `20260520172552_ResourceMembershipTables_20260521` against each Bitcraft database after PR #2 merges. Migration creates `TutorMembers`, **backfills existing `Tutor.OwnerId` rows as self-granted Owner members**, then drops `Tutors.OwnerId`. Verify backfill before running on shared environments.
- Command: `dotnet ef database update --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api`.
- Notes: `Down()` is lossy on multi-Owner tutors and raises if a tutor has no Owner; treat as one-way for any non-dev DB. SQL preview in `.github/plans/feature-freeze-2026-05-21/pr-summaries/02-resource-membership-tables.md`.
- Acceptance/Cleanup: Close after the migration is applied in dev + staging + prod and `TutorMembers` row counts match prior Tutor counts.

- Date: 2026-05-21
- Status: partially complete (build-flag landed)
- Source: Lead Dev (plan v3.4 / D26, D9)
- Target: dev-ops
- Priority: medium
- Task: Config-from-env hardening across the stack. Build-time runtime flag landed via `Directory.Build.props` (`BITCRAFT_RUNTIME_LOCAL_ONLY` guarded on `Configuration == 'Local'`); hardcoded admin credentials removed from `IdentityRoleSeeder` in favor of `BITCRAFT_ADMIN_EMAIL` / `BITCRAFT_ADMIN_PASSWORD`. Broader config-from-env work continues (JWT secret default, connection strings, sidecar URLs, Ollama endpoints).
- Recently completed (2026-05-21): D26 `Directory.Build.props` flag; admin bootstrap env-var gating with skip-and-warn behavior.
- Acceptance/Cleanup: Close when remaining JWT/DB/Ollama/sidecar defaults are also env-driven.

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
