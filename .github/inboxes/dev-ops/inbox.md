---
created_at: 2026-05-19
edited_at: 2026-05-21
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# Dev Ops Inbox

- Date: 2026-05-21
- Status: pending Human review (local-setup-revision-2026-05-21)
- Source: Lead Dev (plan `.github/plans/local-setup-revision-2026-05-21/plan.yaml`)
- Target: dev-ops
- Priority: high
- Task: Smoke-test the revised local container stack on every supported host. Compose set now ships: backend `HEALTHCHECK` on `/api/health` (curl baked into the runtime image), `tutor-studio.depends_on.backend = service_healthy` in dev + prod overrides, `extra_hosts: host.docker.internal:host-gateway` on tutor-studio, refreshed `docker-compose.ai.jetson.yml` (`dustynv/ollama:0.6.8-r36.4-cu126-22.04`, `platform: linux/arm64`, new `ollama-pull-chat` service, curl-based healthcheck), `docker-compose.ai.gpu.yml` annotated with the `gpus: all` shorthand alt, orphan `db/Dockerfile` deleted (HD-2), and new `Taskfile.yml` + `run.ps1` launchers (HD-3). All 6 documented compose combinations pass `docker compose config --quiet` on Windows + Docker Desktop.
- Smoke tests requested: (1) `task up:dev` on Windows + Docker Desktop / WSL2 — verify backend healthy + tutor-studio HMR; (2) `task up:dev` on macOS; (3) `task up:ai-gpu` on Linux + NVIDIA — verify `nvidia-smi` inside ollama container; (4) `task up:ai-jetson` on Jetson Orin — verify both pulls complete + backend reaches Ollama via host-gateway.
- Notes: Setup doc landed at [docs/local-setup.md](../../../docs/local-setup.md); README quickstart now links to it. No Git config changes (HD-4 `.gitattributes` LF normalization remains deferred — separate human approval needed).
- Acceptance/Cleanup: Close after each smoke test passes or files a follow-up item.

- Date: 2026-05-21
- Status: complete (Iteration 1 — pending PR + Human HD answers)
- Source: Secretary (routed from Lead Dev, HD-11 + D41)
- Target: dev-ops
- Priority: complete
- Task: EF migration host-shell fix (D41). Delivered: `src/Bitcraft.Api/appsettings.Development.example.json` (tracked) and new "Database Connection: Compose vs Host Shell" section in `docs/local-setup.md`. **Blocked sub-items routed to Human as HDs**: HD-DO-1 (`.gitignore` add `src/Bitcraft.Api/appsettings.Development.json`), HD-DO-2 (`git rm --cached` the currently-tracked logging override file). Agents cannot edit `.gitignore`.
- Acceptance/Cleanup: Close after PR merges AND Human resolves HD-DO-1/HD-DO-2.

- Date: 2026-05-21
- Status: complete (Iteration 1 audit) — follow-ups queued as HDs
- Source: Secretary (D41 lock 2026-05-21)
- Target: dev-ops
- Priority: complete
- Task: D41 configuration separation audit complete. Compose files clean (env-only). Findings queued for Iteration 3 quality pass: HD-DO-3 (sidecar `requirements-dev.txt` split), HD-DO-4 (track `.env.development.example` + `.env.production.example`), HD-DO-5 (replace `appsettings.json` literal `Password=bitcraft_password` with env-var binding).
- Acceptance/Cleanup: Close after Iteration 3 follow-ups land.


- Source: Lead Dev (plan v3.4 / D25 steps 7-8)
- Target: dev-ops
- Priority: high (release-blocking for any env running merged step 7-8 code)
- Task: Apply EF migration `20260520200241_TutorRevisions_BenchmarkRealign_20260521`. Migration is purely additive: creates `TutorRevisions` (+ partial unique index `IX_TutorRevisions_ActivationSlug WHERE "ActivationSlug" IS NOT NULL`), `TutorRevisionTools`, `TutorDocuments`, `TutorDocumentFolders`; adds `Tutors.ActiveRevisionId` (FK Restrict), `TutorConfigurationSnapshots.TutorRevisionId` (FK Restrict, nullable) + `EffectiveDocumentIdsJson` + `EffectiveKnowledgeBindingJson` (jsonb), and `BenchmarkRuns.TutorRevisionId` (FK Restrict, nullable). No data backfill required; existing rows remain valid because the new FKs are nullable.
- Command: `dotnet ef database update --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api`.
- Notes: Apply only after the prior step 4-8 migration has landed on the target environment. The two migrations are independent but the consolidated step 4-8 migration is the strict prerequisite.
- Acceptance/Cleanup: Close after migration applied in dev + staging + prod.

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
