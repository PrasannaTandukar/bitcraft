---
created_at: 2026-05-21
edited_at: 2026-05-21
creator: lead-dev
editor: lead-dev
ai_gen: true
---
# PR #2 — feature-freeze/02-resource-membership-tables

Second iteration of the Feature Freeze 2026-05-21 plan. Delivers D25 step 2 — Resource-Membership-Tables — and lands D24.a (ABAC obligation vocabulary lock).

## Scope

- Replaces the single-column `Tutor.OwnerId` with a polymorphic resource-membership row table (`TutorMembers`) per **plan.erm.md §3.2** and **D2/D3**.
- Introduces `ResourceRole { Auditor=0, Editor=1, Owner=2 }` as a resource-scoped role concept distinct from the global `AppRoles`.
- Adds `ITutorMembershipReader` (Application interface) and an EF-backed implementation in Infrastructure as the seam every Application handler now reads through.
- Replaces every prior `tutor.OwnerId != currentUser.UserId` check across nine feature files with role-typed membership checks (Owner|Editor for mutations, any non-null role for reads).
- Generates a single migration `ResourceMembershipTables_20260521` with **safe in-place backfill** before the `OwnerId` column drop, so existing data survives a `database update`.
- Persists **D24.a** (ABAC obligation vocabulary) in `plan.decisions.md` and `plan.yaml.locked_decisions`; the obligation evaluator itself is not exercised by this step.

## Locked decisions referenced

| ID | Concern | Use here |
| -- | ------- | -------- |
| **D2** | Per-resource membership replaces single-owner column | Drives the `TutorMember` shape and `ResourceRole` enum. |
| **D3** | Resource role vocabulary (Auditor < Editor < Owner) | Persisted int ordering locked by `ResourceRoleTests`. |
| **D24** | Centralized ABAC policy evaluator location | Step 1 (already merged); this iteration intentionally bypasses the evaluator and uses the role-typed reader directly because membership *is* the attribute being authored. |
| **D24.a** | ABAC obligation vocabulary (minimum-viable) | Newly locked in this PR — see `plan.decisions.md` §D24.a and `plan.yaml.locked_decisions`. No code yet consumes the vocabulary; it gates downstream WS6 policy authors. |
| **D25 step 2** | 8-step migration sequence | This iteration is exactly step 2 of the locked sequence. |

## Files

### New (5)

- `src/Bitcraft.Domain/Tutoring/ResourceRole.cs` — int-backed enum with locked ordering.
- `src/Bitcraft.Application/Common/Interfaces/ITutorMembershipReader.cs` — read seam over membership.
- `src/Bitcraft.Infrastructure/Persistence/Configurations/Tutoring/TutorMemberConfiguration.cs` — EF mapping (unique `(TutorId, UserId)` index, two restrict-delete FKs to `AspNetUsers`, one cascade FK to `Tutors`).
- `src/Bitcraft.Infrastructure/Persistence/Repositories/Tutoring/TutorMembershipReader.cs` — EF-backed reader, `AsNoTracking()` on list path.
- `src/Bitcraft.Infrastructure/Persistence/Migrations/20260520172552_ResourceMembershipTables_20260521.cs` — schema + backfill migration.
- `tests/Bitcraft.UnitTests/Domain/Tutoring/ResourceRoleTests.cs` — locks persisted int ordering.

### Modified (≈18)

- `src/Bitcraft.Domain/Tutoring/Tutor.cs` — dropped `OwnerId`/`Owner`; added `Members` collection, `static Create(name, description, foundingOwnerUserId)` factory, `WouldLeaveNoOwner(memberId)` invariant helper; `TutorMember` is aggregate-local in this same file.
- `src/Bitcraft.Application/Features/Tutoring/*.cs` (7 files) — every owner check replaced by `await membership.GetRoleAsync(...)`. Minimum-role enforced per operation:
  - **Read** handlers (GetTutorById, list/search): any non-null role permitted.
  - **Write** handlers (Update, SavePromptVersion, SaveTutorModelConnection, RegisterToolDefinition, UpsertDeploymentProfile, CaptureConfigurationSnapshot, LinkKnowledgeSource): require Owner or Editor.
  - Future grant/revoke handlers (not in this PR): will require Owner.
- `src/Bitcraft.Application/Features/Knowledge/KnowledgeFeatures.cs` — `SearchKnowledge` switched to membership reader.
- `src/Bitcraft.Application/Features/Benchmarking/BenchmarkFeatures.cs` — four sites switched (CreateRun, ListRuns, GetRun, GetRunProgress). `BenchmarkTemplate.OwnerId` left intact (different aggregate; D25 step 8).
- `src/Bitcraft.Infrastructure/Persistence/AppDbContext.cs` — `DbSet<TutorMember>`.
- `src/Bitcraft.Infrastructure/Persistence/Configurations/Tutoring/TutorConfiguration.cs` — dropped owner mapping/indexes; added `HasMany(Members) … OnDelete(Cascade)`.
- `src/Bitcraft.Infrastructure/Persistence/Repositories/Tutoring/TutorRepository.cs` — `ListByOwnerAsync` rewritten as `Members.Any(m => m.UserId == ownerId)`.
- `src/Bitcraft.Infrastructure/DependencyInjection.cs` — registered `ITutorMembershipReader` (scoped).
- `src/Bitcraft.Api/Contracts/Tutoring/Tutors.cs` — removed `OwnerId` from `TutorResponse`.
- `src/Bitcraft.Api/Mapping/TutoringMapping.cs` — Tutor mapping updated.
- `tests/Bitcraft.UnitTests/Application/Tutoring/Fakes/FakeRepositories.cs` — `FakeTutorMembershipReader` added; `ListByOwnerAsync` fake updated.
- `tests/Bitcraft.UnitTests/Domain/Tutoring/TutorDomainTests.cs` — six new invariant tests (`Create_*`, `WouldLeaveNoOwner_*`).
- `tests/Bitcraft.UnitTests/Application/Tutoring/TutoringFeaturesTests.cs` — handler ctor calls and Tutor construction migrated.
- `tests/Bitcraft.UnitTests/Application/Benchmarking/BenchmarkFeaturesTests.cs` — same migration; ten sites.
- `tests/Bitcraft.InfrastructureIntegrationTests/Persistence/TutorRepositoryTests.cs` — five sites migrated.
- `.github/plans/feature-freeze-2026-05-21/plan.decisions.md` — appended D24.a, bumped frontmatter.
- `.github/plans/feature-freeze-2026-05-21/plan.yaml` — D24.a in `locked_decisions`.

## Validation

| Gate | Result |
| ---- | ------ |
| `dotnet build bitcraft.slnx` | 0 errors / 0 warnings |
| `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj` | 136 / 136 passed (+8 vs PR #1: 6 in `TutorDomainTests`, 2 in `ResourceRoleTests`) |
| Integration tests | **Not run** — Human owns; requires Docker DB. |
| `dotnet ef database update` | **Not run** — agent boundary; Human owns. |
| Cleanup grep | Zero `Tutor.OwnerId` references remain in `src/` and `tests/`. `BenchmarkTemplate.OwnerId` is legitimately untouched (D25 step 8 territory). |

## Migration notes

Generated migration: `20260520172552_ResourceMembershipTables_20260521`.

**Critical ordering** in `Up()` — table created and backfill executed *before* the `OwnerId` column drop:

```sql
-- (1) create TutorMembers + indexes
CREATE TABLE "TutorMembers" ( ... );
CREATE UNIQUE INDEX "IX_TutorMembers_TutorId_UserId" ON "TutorMembers" ("TutorId", "UserId");
CREATE INDEX "IX_TutorMembers_UserId" ON "TutorMembers" ("UserId");
CREATE INDEX "IX_TutorMembers_GrantedById" ON "TutorMembers" ("GrantedById");

-- (2) backfill: every existing Tutor.OwnerId becomes a self-granted Owner TutorMember.
--     NOT EXISTS guard makes the statement idempotent so re-running is safe.
INSERT INTO "TutorMembers" ("Id", "TutorId", "UserId", "Role", "GrantedUtc", "GrantedById")
SELECT gen_random_uuid(),
       t."Id",
       t."OwnerId",
       2,  -- ResourceRole.Owner (locked by ResourceRoleTests)
       COALESCE(t."CreatedUtc", NOW() AT TIME ZONE 'UTC'),
       t."OwnerId"
FROM "Tutors" t
WHERE t."OwnerId" IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM "TutorMembers" m
      WHERE m."TutorId" = t."Id" AND m."UserId" = t."OwnerId"
  );

-- (3) only now drop the legacy column
ALTER TABLE "Tutors" DROP CONSTRAINT "FK_Tutors_AspNetUsers_OwnerId";
DROP INDEX "IX_Tutors_OwnerId";
DROP INDEX "IX_Tutors_OwnerId_Name";
ALTER TABLE "Tutors" DROP COLUMN "OwnerId";
```

- `gen_random_uuid()` is a PostgreSQL built-in since v13 (no extension load needed; pgvector already implies PG ≥ 14).
- `Down()` restores `OwnerId` nullable, reverse-backfills the earliest-granted Owner per tutor with `DISTINCT ON`, fails fast (via `RAISE EXCEPTION`) if any tutor has no Owner row, then re-applies `NOT NULL`. **Lossy on multi-Owner tutors**; documented inline.

## Risks & follow-ups

- **No grant / revoke endpoints yet.** This PR delivers the read-only authorization path; the only path that creates membership today is `Tutor.Create` (founding Owner). A grant/revoke API surface is required before the ≥1-Owner invariant becomes user-visible. Tracked as a follow-up; not blocking step 3 (RuntimeEngine-Entity) since no step-3 endpoint mutates membership.
- **`TutorMember.GrantedById` for backfilled rows is self-grant.** No external attribution is available at migration time; documented in the migration body. Acceptable for the PoC.
- **`Down()` is lossy** for tutors that have multiple Owners (only earliest survives) and **raises** if a tutor has no Owner. Acceptable: Down is for local dev rollback, not production.
- **`ITutorMembershipReader` does not yet emit ABAC obligations.** Per D24.a's note ("Does not block WS2 step 2 — uses the boolean/role-typed reader, not the obligation-carrying evaluator"), this is by design. The obligation-bearing `IPolicyEvaluator` path will be retrofitted in WS6.
- **Step 1 housekeeping commit `fb5aa50`** (PR #1 summary + 3 skill folders) lives only on `feature-freeze/skills` and did not land in the merged step 1 PR #23. Decision needed: re-introduce here in a follow-up commit, or on a dedicated branch.

## Human action items

1. Review the SQL preview above and the migration file directly.
2. Run `dotnet ef database update --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api` against a **dev** database first and inspect the resulting `TutorMembers` rows for backfill correctness. **Do not** run against shared/staging without a backup.
3. Push branch `feature-freeze/02-resource-membership-tables` and open PR #2 targeting `feature-freeze-2026-05-21`.
4. Decide on the step 1 housekeeping commit handling (see Risks).
5. Schedule the grant / revoke API surface for a follow-up iteration before step 3 ships any UI that needs multi-member tutors.
