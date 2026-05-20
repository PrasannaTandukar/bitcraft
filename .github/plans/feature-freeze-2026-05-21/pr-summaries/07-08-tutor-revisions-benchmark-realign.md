---
created_at: 2026-05-21
edited_at: 2026-05-21
creator: lead-dev
editor: lead-dev
ai_gen: true
---
# PR #7-8 (combined) — feature-freeze/05-tutor-document-mn

Consolidated delivery of D25 steps 7 + 8 on the same working branch as the prior consolidated batch. User authorized merging steps 7 and 8 into a single backend delivery so the frontend step (D25 step 9) can pick up a stable surface area.

## Scope

- **D25 step 7 — TutorRevision aggregate (plan.erm.md §7).** Introduces `TutorRevision` as the addressable unit of tutor versioning. Each revision composes prompt version + interaction model + optional query embedding model + per-revision tools + interaction parameters + deployment config. State machine: Draft → Published → Active; Published/Active → Archived; Draft is hard-deletable. Activation slug is unique partial (`WHERE "ActivationSlug" IS NOT NULL`) per D22.b, with a one-revision redirect window via `PreviousActivationSlug`.
- **D25 step 7 — Tutor.ActiveRevisionId pointer.** `Tutor` gains nullable `ActiveRevisionId` FK (Restrict) mirroring the row whose Status is Active.
- **D25 step 7 — TutorDocument / TutorDocumentFolder.** New M:N tables join `Tutor` directly to `Document` (`AttachedVia` = Direct | Folder) and to per-user `DocumentFolder`. Legacy `KnowledgeSourceLink` is retained alongside this new surface — drop/migrate of the legacy table is deferred to a future step.
- **D25 step 7 — Effective-set materialization at activation.** `IRevisionEffectiveSetReader` resolves the union of direct + folder-expanded documents and the per-document `(EmbeddingModelId, Dimensions)` binding. The resolved sets are captured into the snapshot at activation time.
- **D25 step 7 — Snapshot realignment.** `TutorConfigurationSnapshot` gains nullable `TutorRevisionId` FK (Restrict) + `EffectiveDocumentIdsJson` + `EffectiveKnowledgeBindingJson` (jsonb). Legacy snapshots remain valid because `TutorRevisionId` is nullable; only revision-driven activations populate it.
- **D25 step 8 — BenchmarkRun.TutorRevisionId.** `BenchmarkRun` gains nullable `TutorRevisionId` FK (Restrict). Existing benchmark rows remain valid; new runs may pin to a revision once the runtime is upgraded.
- **API surface.** `TutorRevisionsController` under `/api/tutors/{tutorId}/revisions` with list / get-by-id (`AuditorOrAbove`), and create-draft / update-draft / publish / activate / archive / discard (`EditorOrAdmin`). Activation generates a slug from `Tutor.Name`, archives the prior active revision atomically, captures the snapshot, and updates `Tutor.ActiveRevisionId`.

## Files

- New domain: `src/Bitcraft.Domain/Tutoring/RevisionStatus.cs`, `TutorRevision.cs`, `TutorDocument.cs`.
- Modified domain: `Tutor.cs` (+ `ActiveRevisionId`, `Revisions`, `TutorDocuments`, `TutorDocumentFolders`), `TutorConfigurationSnapshot.cs` (+ `TutorRevisionId`, `EffectiveDocumentIdsJson`, `EffectiveKnowledgeBindingJson`, extended `Capture(...)` with three optional trailing params), `Benchmarking/BenchmarkRun.cs` (+ `TutorRevisionId`).
- New EF configs: `Persistence/Configurations/Tutoring/TutorRevisionConfiguration.cs`, `TutorDocumentConfiguration.cs`.
- Modified EF configs: `Tutoring/TutorConfiguration.cs`, `Tutoring/TutorConfigurationSnapshotConfiguration.cs`, `Benchmarking/BenchmarkRunConfiguration.cs`.
- New Application: `Common/Interfaces/IRevisionEffectiveSetReader.cs`, `Features/Tutoring/TutorRevisionFeatures.cs`. Extended `ITutorRepository` with eight revision methods.
- New Infrastructure: `Persistence/Repositories/Tutoring/RevisionEffectiveSetReader.cs`. Extended `TutorRepository` + DI registration.
- New API: `Contracts/Tutoring/TutorRevisions.cs`, `Controllers/Tutoring/TutorRevisionsController.cs`, mapping extension on `TutoringMapping`.
- New tests: `tests/Bitcraft.UnitTests/Domain/Tutoring/TutorRevisionDomainTests.cs` (10 state-machine cases). Extended `FakeRepositories.cs` with revision storage.
- New migration: `Persistence/Migrations/20260520200241_TutorRevisions_BenchmarkRealign_20260521.cs` (+ Designer).

## Validation

- `dotnet build bitcraft.slnx` — clean, 0 errors / 0 warnings.
- `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj` — 165 passed / 0 failed / 0 skipped (10 new revision state-machine tests).
- EF migration scaffolded successfully; partial unique index `IX_TutorRevisions_ActivationSlug` emitted with the expected `"ActivationSlug" IS NOT NULL` filter.
- Database update not executed (Dev Ops / human applies migrations).
- API integration / browser validation not run for this batch; left to the next batch covering the frontend slice.

## Deferred / known follow-ups

- `KnowledgeSourceLink` legacy table retained alongside the new `TutorDocument` M:N surface. Migration of legacy rows + drop of the legacy table is left to a follow-up step.
- Snapshot `TutorRevisionId` is nullable so legacy pre-revision snapshots remain valid. Tightening to NOT-NULL after backfill is a future cleanup.
- `BenchmarkRun.TutorRevisionId` is nullable for the same reason; benchmarking worker code does not yet pin runs to revisions automatically.
- Tools attached at the revision level are written via the EF model only; no API yet exposes attach/detach for `TutorRevisionTool` — `ToolDefinition` continues to be tutor-scoped under the existing controller. A future step can move tool definitions to revision-scoped if desired.
- Activation flow archives the prior active revision in-process but does not yet wrap the multi-step operation in an explicit DB transaction. The two `UpdateRevisionAsync` calls plus the snapshot + tutor update each call `SaveChangesAsync` independently; failure recovery is by re-running activation. A future cleanup can wrap this in `IExecutionStrategy.ExecuteInTransactionAsync`.

## Operational notes for Dev Ops

- Run `dotnet ef database update --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api` against the target environment to apply the new migration. The migration is purely additive — new tables (`TutorRevisions`, `TutorRevisionTools`, `TutorDocuments`, `TutorDocumentFolders`), new columns on `Tutors` / `TutorConfigurationSnapshots` / `BenchmarkRuns`, and a new partial unique index. No data is deleted or rewritten.
- A separate Lead Dev inbox item already tracks the prior batch's EF "no such host" + dev compose credentials validation; that work is still the blocker for actually applying any migration locally.
