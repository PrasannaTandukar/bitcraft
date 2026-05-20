---
created_at: 2026-05-20
edited_at: 2026-05-20
creator: lead-dev
editor: lead-dev
ai_gen: true
---
# Feature Freeze 2026-05-21 — Development Kickoff Brief

> Owner: Lead Dev. Companion to [plan.md](plan.md) v3.4, [plan.erm.md](plan.erm.md), [plan.decisions.md](plan.decisions.md), [plan.yaml](plan.yaml).
>
> Purpose: surface every decision and conflict the Human must resolve in one pass so WS2 step 1 (D25 step 1, `Identity-And-Roles-Vocabulary`) can start the moment a branching strategy is picked. This file is a planning artifact, not source code.

## 1. Branching Strategy Recommendation

The Human raised three options. Each is reviewed against the locked 8-step migration sequence (D25) plus the frontend TS migration (D18.a) which together produce ~9 distinct iterations.

| Option | Shape | Pros | Cons |
|---|---|---|---|
| **A. PR plan to `dev`, branch each iteration from `dev`** | Every D25 step branches off `dev` and PRs back to `dev` directly. | Simple mental model; no integration branch to maintain. | Each PR exposes `dev` to a partial schema state. Rollback of step *n* after step *n+1* lands requires surgery on `dev`. Mid-sequence breakage (e.g. WS5 dynamic dims) hits `dev` immediately. |
| **B. Branch iterations from `feature-freeze` sequentially, PR each iteration into `dev`** | Iterations stack on `feature-freeze` but PR around it into `dev`. | Keeps step branches small. | Worst of both worlds: `feature-freeze` accumulates state that has not been reviewed-as-merged anywhere, and `dev` still sees per-step intermediate states. PR review confusion (base branch vs target branch mismatch). |
| **C. Branch iterations from `feature-freeze`, PR each into `feature-freeze`, final `feature-freeze` → `dev`** *(recommended)* | Each D25 step is one PR into `feature-freeze`. `feature-freeze` is the integration branch. Final `feature-freeze` → `dev` merge once CI is green at the tip. | Per-step review is small and bounded. `feature-freeze` accumulates a coherent, reviewed state. Rollback is `git reset` on the integration branch, not surgery on `dev`. `dev` only ever sees a single feature-freeze-complete merge. | `feature-freeze` lives longer and accumulates a stack of merged branches. Final dev merge gates on CI green at `feature-freeze` tip (currently no CI — see §5). |

### Recommendation: Option C with stacked-PR discipline

- Each of the 8 D25 backend steps is one PR into `feature-freeze`.
- The frontend TS migration (D18.a) lands as additional PRs into `feature-freeze` after backend contract surfaces are stable enough (D18 gate in [plan.frontend.md §9](plan.frontend.md)).
- Final `feature-freeze` → `dev` is **one** merge. Recommendation: **squash merge** so `dev` history captures a single semantic "feature freeze 2026-05-21" commit; per-step history remains visible in the `feature-freeze` ref history.
- Within `feature-freeze`, recommendation: **squash merge** each step PR for the same reason — one revision = one commit on the integration branch, readable in `git log`.

### Proposed branch naming (per D25 step order)

```
feature-freeze/01-identity-and-roles-vocabulary
feature-freeze/02-resource-membership-tables
feature-freeze/03-runtime-engine-entity
feature-freeze/04-model-tph-hierarchy
feature-freeze/05-tutor-document-mn
feature-freeze/06-knowledge-dynamic-dimensions
feature-freeze/07-tutor-revisions
feature-freeze/08-benchmark-fk-realign
feature-freeze/09-frontend-typescript-migration
```

The frontend migration sub-order (Settings → ModelCatalog → Tutors → Files → Benchmarking, per D18.a) may either share branch `09-...` with sequential commits or split into `09a-`, `09b-`, … sub-branches. **Recommendation: split** so each page's PR is reviewable independently against the corresponding backend contract.

## 2. Human ↔ Agent Responsibility Split

This table applies to **every** iteration cycle. Locked Autopilot rules in [.github/copilot-instructions.md](../../copilot-instructions.md) and [AGENTS.md](../../../AGENTS.md) are the source of truth; this is a working summary.

| Action | Owner | Notes |
|---|---|---|
| Pick branching strategy and confirm naming | Human | One-time, gates step 1 start. |
| Create iteration branch | Human | `git checkout -b feature-freeze/01-identity-and-roles-vocabulary` from `feature-freeze`. |
| Draft entity / contract / handler / EF configuration / test skeleton code | Agent (Lead Dev → NET Dev / Web Dev) | Written into working tree on the active branch. |
| Supply package list to add/remove | Agent | List provided; Human runs `dotnet add package` / `npm install`. |
| Run `dotnet add package` / `npm install` / `npm ci` | Human | Per Autopilot-Locked Actions. |
| `dotnet build bitcraft.slnx` | Agent | Basic compile validation after edits. |
| `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj` | Agent | Scoped unit tests for the step. |
| Run `dotnet ef migrations add <Name>` | Human | Agent supplies the migration name; Human executes per Autopilot-Locked Actions. |
| `dotnet ef database update` (any environment, dev or shared) | Human | Database destructive operations are Autopilot-Locked. |
| Run integration tests (API + Infrastructure + Sidecar) | Human | Agents do not exercise running DBs, sidecars, or Ollama. |
| Frontend lint / vitest / dev server | Agent for lint/vitest; Human for `npm run dev` smoke | Local-only frontend work. |
| `git add` / `git commit` / `git push` / PR creation | Human | All Git writes are Autopilot-Locked. |
| Code review (draft) | Agent (Sec Ops for ABAC / Auth surface; Lead Dev for cross-cutting; AI Ops for any `.github/` change) | Drafted as review comments in the working tree or chat. |
| Code review (approval) | Human | Only Human approves merges. |
| Inbox/plan updates after step lands | Agent (Secretary or owning expert) | Per `.github/inboxes/<agent>/inbox.md`. |
| Restore archived plan files, modify `.gitignore`, ignore-files, or `ai_gen: false` frontmatter | Human only | Autopilot-Locked. |

## 3. WS2 Step 1 — Identity-And-Roles-Vocabulary File-Touch Map

Step 1 of D25. **Scope**: rename Viewer → Auditor, remove hardcoded admin seed, introduce centralized ABAC policy evaluator interface and types (D24), prepare DI registration site for the Infrastructure adapters (no implementation yet), prepare migration entry for any persisted role rename. **Out of scope for step 1**: actual policy evaluator implementation, attribute-fetching adapters, controller rewrites to use evaluator (those land progressively with WS6/WS7), `TutorMember` (that is step 2), `Document.Visibility` schema (that lands with WS4 / step 5).

### 3.1 Domain layer — verify, no schema changes

- [src/Bitcraft.Domain/Identity/AppUser.cs](../../../src/Bitcraft.Domain/Identity/AppUser.cs) — verified: vanilla `IdentityUser<Guid>` shape. **No edit** for step 1. D1.a `Document.Visibility` is a different aggregate and lands later. No coarse subject-attribute columns are added in step 1; the subject attributes are derived from JWT claims + `UserManager.GetRolesAsync` at policy-evaluation time.
- [src/Bitcraft.Domain/Identity/AppRole.cs](../../../src/Bitcraft.Domain/Identity/AppRole.cs) — verified: vanilla `IdentityRole<Guid>` shape. **No edit** for step 1.

### 3.2 Application layer

| File | Status | Contents (contract-level) |
|---|---|---|
| [src/Bitcraft.Application/Common/Security/AppRoles.cs](../../../src/Bitcraft.Application/Common/Security/AppRoles.cs) | edit | Replace `Viewer` constant with `Auditor`. Update `All` collection to `[Admin, Editor, Auditor]`. Per D1, these remain **coarse subject attributes**, not access decisions. |
| [src/Bitcraft.Application/Common/Security/AuthorizationPolicies.cs](../../../src/Bitcraft.Application/Common/Security/AuthorizationPolicies.cs) | edit | Rename `ViewerOrAbove` policy constant to `AuditorOrAbove`. Update every referencing controller (verified by build error sweep — should be a small, mechanical pass). |
| `src/Bitcraft.Application/Common/Authorization/` | new folder | Per D24, the centralized ABAC evaluator lives here. |
| `src/Bitcraft.Application/Common/Authorization/IPolicyEvaluator.cs` | new | Public interface. Method shape (signature only, no body): `Task<PolicyDecision> EvaluateAsync(PolicyRequest request, CancellationToken ct)`. `PolicyRequest` is a record carrying subject claims/roles, resource type tag, resource id (optional), action verb, and environment attributes (e.g. build-flag, request scheme). Implementations live in `Bitcraft.Infrastructure.Authorization` (not introduced in step 1). |
| `src/Bitcraft.Application/Common/Authorization/PolicyDecision.cs` | new | Immutable record per D24: `PolicyDecision(PolicyEffect Effect, IReadOnlyList<Obligation> Obligations, IReadOnlyList<string> Reasons)`. `PolicyEffect` enum: `Permit`, `Deny`, `NotApplicable`. |
| `src/Bitcraft.Application/Common/Authorization/Obligation.cs` | new | Immutable record `Obligation(string Kind, IReadOnlyDictionary<string, string> Parameters)`. `Kind` is open enum (e.g. `"audit-log"`, `"redact-fields"`) — values are chosen by Sec Ops as the policy catalog grows. |
| `src/Bitcraft.Application/Common/Authorization/PolicyRequest.cs` | new | Immutable record `PolicyRequest(ClaimsPrincipal Subject, string ResourceType, Guid? ResourceId, string Action, IReadOnlyDictionary<string, object?> Environment)`. Application-layer use of `ClaimsPrincipal` is allowed because it lives in `System.Security.Claims`, not a web framework boundary. |

### 3.3 Infrastructure layer — placeholder only

- `src/Bitcraft.Infrastructure/Authorization/` — **new folder**, empty at end of step 1 except for a short `README.md` (or the folder is created by the first file that lands in step-1+n; recommendation: create folder with a single stub class `PolicyEvaluatorPlaceholder` that throws `NotImplementedException("Implementation pending for this capability.")` per repo convention, registered as `IPolicyEvaluator` so DI graph compiles and tests do not fail at composition root). Concrete evaluator lands progressively with WS6/WS7 attribute resolvers.
- [src/Bitcraft.Infrastructure/DependencyInjection.cs](../../../src/Bitcraft.Infrastructure/DependencyInjection.cs) — edit. Register `IPolicyEvaluator` → `PolicyEvaluatorPlaceholder` (scoped). Update existing `ViewerOrAbove` policy registration to `AuditorOrAbove` (this is the rename half of step 1).
- [src/Bitcraft.Infrastructure/Identity/IdentityRoleSeeder.cs](../../../src/Bitcraft.Infrastructure/Identity/IdentityRoleSeeder.cs) — edit. **Remove** the hardcoded `admin@bitcraft.com` / `Admin1234!` block. Keep idempotent role seeding only. (Note: this is the WS1 hardcoded-credentials removal item from [plan.md WS1](plan.md#ws1---bootstrap-template-and-role-vocabulary). It lands in step 1 because it shares the Identity migration and is the same review surface. The full `BootstrapOptions` template work from WS1 is not in step 1; it lands as a follow-up before step 2 or as part of step 1's PR, Human's call — see §5 ambiguity.)

### 3.4 API layer — no controller changes

No controller code changes in step 1 beyond mechanical rename of `[Authorize(Policy = "ViewerOrAbove")]` → `[Authorize(Policy = "AuditorOrAbove")]`. The centralized evaluator is not yet wired into controllers; that integration happens incrementally as WS6/WS7 land.

### 3.5 Tests

| File | Status | Contents (placeholder) |
|---|---|---|
| `tests/Bitcraft.UnitTests/Application/Common/Security/AppRolesTests.cs` | new | Assert `AppRoles.All` equals `[Admin, Editor, Auditor]`; no `Viewer` constant exists. |
| `tests/Bitcraft.UnitTests/Application/Common/Authorization/PolicyDecisionTests.cs` | new | Assert record equality semantics and that `Effect` defaults are not silently `Permit`. |
| `tests/Bitcraft.ApiIntegrationTests/AuthenticationPolicyTests.cs` | edit | Replace any `ViewerOrAbove` references with `AuditorOrAbove`. Add a smoke test that a user in `Auditor` role can hit an `AuditorOrAbove`-gated endpoint. |
| `tests/Bitcraft.InfrastructureIntegrationTests/IdentityRoleSeederTests.cs` | new (or extend existing) | Assert role-only seeding is idempotent and that **no** default admin user is created when no bootstrap section is present. |

### 3.6 Migration

- Migration name (Human runs): **`IdentityAndRolesVocabulary`** (timestamp auto-prefixed by `dotnet ef migrations add`).
- Expected EF-generated effect: an `Up` that renames any persisted `AspNetRoles` row whose `Name = 'Viewer'` to `Auditor` (and `NormalizedName`). Because role names are stored as data, EF will likely **not** emit this rename automatically; the migration will need a manual `migrationBuilder.Sql(...)` block. **NET Dev to draft the SQL** as part of step 1 PR; Human executes `dotnet ef migrations add IdentityAndRolesVocabulary` and inspects the generated file before committing.
- Down migration: reverse `Auditor` → `Viewer`.
- Sequence position: this becomes the **6th** migration after the existing 5 (see [§6](#6-critical-conflicts-or-risks-to-flag-now)).

## 4. Pre-Development Items Doable Now (No Branch Required)

The Human asked for items agents can prepare on the working tree before the iteration branch exists. Agents draft text only; Human carries the work onto the branch.

### 4.1 `Directory.Build.props` draft (D26)

Verified: **no `Directory.Build.props` exists anywhere in the repo today**. D26 places it at **repo root** (recommended — confirms with §5 ambiguity).

Drafted XML for D26 (`BITCRAFT_RUNTIME_LOCAL_ONLY`) — Human places this file at `c:\Users\M\Desktop\projects\bitcraft\Directory.Build.props` when ready, **after** confirming the location (§5):

```xml
<Project>
  <!--
    D26 — BITCRAFT_RUNTIME_LOCAL_ONLY definition site.
    Defined ONLY when MSBuild Configuration == 'Local'.
    CI never sets 'Local'. Debug and Release builds do NOT define this symbol.
    When defined, runtime chat endpoints may permit anonymous local-only access
    (per D8). When undefined (the only state for shipped builds), JWT is always
    required.
  -->
  <PropertyGroup Condition="'$(Configuration)' == 'Local'">
    <DefineConstants>$(DefineConstants);BITCRAFT_RUNTIME_LOCAL_ONLY</DefineConstants>
  </PropertyGroup>
</Project>
```

Notes:
- Empty default `PropertyGroup` is intentionally omitted; nothing else is set globally. Adding cross-project compile settings (e.g. `TreatWarningsAsErrors`) is **out of scope** for step 1 and would need its own Human decision.
- `Local` is a non-standard MSBuild configuration; Human will need to add `Local` to the `.slnx` configuration list or build with `dotnet build -c Local` once Local-mode work begins (WS7). Agents do not modify `bitcraft.slnx`.
- No source file uses `#if BITCRAFT_RUNTIME_LOCAL_ONLY` yet; the symbol's first consumer is WS7's runtime controller. Defining the symbol now does not change build output until then.

### 4.2 Sec Ops ABAC policy catalog (paper artifact)

Verified: **no `.github/governance/abac-policies.md` exists today**. Per the user request, this brief drafts the contents only; the file is **not created** unless a follow-up explicitly instructs it.

Proposed draft contents for `.github/governance/abac-policies.md` (frontmatter: `creator: sec-ops`, `editor: sec-ops`, `ai_gen: true`):

- **Section 1 — Purpose**: enumerate the ABAC policies the centralized evaluator (D24) recognizes; this file is the human-readable counterpart to `IPolicyEvaluator`'s decisions.
- **Section 2 — Subject attributes**: `User.Id`, `User.GlobalRole` (Admin/Editor/Auditor, per D1 coarse subject attribute), `User.IsAuthenticated`.
- **Section 3 — Resource attributes (current)**: `Tutor.MemberRoleForUser` (via `TutorMember`, lands in step 2), `TutorRevision.Status` (lands in step 7), `Document.Visibility` (Internal default; lands in step 5).
- **Section 4 — Environment attributes**: `Build.BITCRAFT_RUNTIME_LOCAL_ONLY` (D26), `Request.IsLoopback` (defense-in-depth even though D8 makes anonymous a build-only path).
- **Section 5 — Policies (initial catalog)**: `tutor.read`, `tutor.write`, `tutor.activate`, `tutor.member.manage`, `document.attach`, `document.detach`, `document.read`, `knowledge.rebuild`, `model.admin`, `runtimeEngine.admin`, `runtime.ask` (each row: subject preconditions, resource preconditions, environment preconditions, obligations).
- **Section 6 — Obligations**: enumerate the initial `Obligation.Kind` values (`audit-log`, `redact-fields`) and what consuming handlers must do when they receive each.
- **Section 7 — Review process**: Sec Ops is the reviewer for any new policy or attribute; AI Ops is the reviewer for any change to `.github/governance/`.

**Recommendation**: defer creation of this file until Sec Ops is engaged in step 2 (where `TutorMember` makes the first resource-attribute concrete). Authoring it before any real attribute exists risks the catalog drifting from the implementation.

### 4.3 NuGet packages needed for step 1

Verified from `.csproj` files:

- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.5 — present in [Bitcraft.Infrastructure.csproj](../../../src/Bitcraft.Infrastructure/Bitcraft.Infrastructure.csproj).
- `Microsoft.EntityFrameworkCore` 10.0.5, `Microsoft.EntityFrameworkCore.Design` 10.0.5 — present.
- `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.5 — present.
- `MediatR` 14.1.0 — present in [Bitcraft.Application.csproj](../../../src/Bitcraft.Application/Bitcraft.Application.csproj).
- `System.Security.Claims` — part of the BCL, no package reference needed for `ClaimsPrincipal` in `PolicyRequest`.

**Conclusion: step 1 needs zero new NuGet packages.** Human does not need to run `dotnet add package` for step 1. New packages will first be needed in step 3 (RuntimeEngine — none expected) or step 6 (Knowledge dynamic dims — `Pgvector.EntityFrameworkCore` already present).

## 5. Ambiguities And Questions For Human (Single Decision Pass)

The Human should answer these in **one** pass so step 1 is unblocked. Recommendations are flagged clearly.

1. **Branching strategy.** Pick A, B, or C. **Recommendation: C with squash merges** at both the per-step and final-integration layers. See [§1](#1-branching-strategy-recommendation).
2. **Frontend timing.** Reconfirm WS9 (TypeScript migration, D18.a) starts **after** backend contracts stabilize per [plan.frontend.md §9](plan.frontend.md) gate. **Recommendation: confirmed, do not parallelize** — D27 explicitly defers OpenAPI codegen until contracts hold shape, so running frontend conversion in parallel risks rework as backend DTOs evolve through steps 2–8.
3. **`Directory.Build.props` location.** **Recommendation: repo root**, next to `bitcraft.slnx`. The MSBuild "Directory.Build.props" convention walks **up** from each project; a single root-level file applies to everything (Api, Application, Domain, Infrastructure, tests). Placing it under `src/` would not apply to the tests projects. Confirm or override.
4. **Per-step PR merge style into `feature-freeze`.** **Recommendation: squash**, for review-history clarity and one-commit-per-revision on the integration branch. Confirm or pick fast-forward / merge-commit.
5. **CI hooks on `feature-freeze`.** Verified: **`.github/workflows/` does not exist**. There is currently no automated build/test on PR. **Recommendation: defer CI hookup until WS9 freeze validation**, but the Human should confirm — running 8 sequential migrations without CI green-light puts the burden on agents (build + unit tests) and Human (integration + smoke) every step. If the Human wants CI before the stack begins, that becomes a Dev Ops mini-iteration ahead of step 1.
6. **Missing agents/skills for step 1.** The AI Ops MCP review surfaced a potential `bitcraft-abac-policy-authoring` skill. **Step 1's centralized evaluator is a code seam, not a policy catalog**, so the skill is not strictly required to start. **Recommendation: author the skill before step 2** (where `TutorMember`'s first ABAC policy lands) rather than before step 1. Confirm.
7. **`kickoff.md` scope.** This file lives under `.github/plans/feature-freeze-2026-05-21/` alongside the other plan artifacts, with `ai_gen: true`. **Recommendation: keep here**. It is a planning artifact, not governance doctrine, and follows the same archive lifecycle as `plan.md`. Confirm or instruct relocation.
8. **WS1 bootstrap template scope in step 1 PR.** Removing the hardcoded admin (WS1) is mechanical and shares the Identity review surface, so it fits step 1. The full WS1 `BootstrapOptions` + `template.appsettings.Bootstrap.json` work is larger. **Recommendation: split — remove hardcoded credentials in step 1; `BootstrapOptions` template lands as step 1.5 (a short, isolated PR into `feature-freeze`) before step 2.** Confirm or instruct to fold both into step 1.

## 6. Critical Conflicts Or Risks To Flag Now

Findings from a workspace sweep before any step 1 code is written.

### 6.1 Existing Identity shapes — clean

- `AppUser` and `AppRole` are vanilla `IdentityUser<Guid>` / `IdentityRole<Guid>` with no Bitcraft-specific columns. **No conflict** with the D1 coarse subject-attribute story; subject attributes come from JWT claims + Identity role membership at evaluation time, not from new columns. Step 1 adds no Domain schema.

### 6.2 Existing Infrastructure policy registration — single rename only

- [Bitcraft.Application/Common/Security/AppRoles.cs](../../../src/Bitcraft.Application/Common/Security/AppRoles.cs) defines `Admin`, `Editor`, `Viewer`. **Mechanical rename to `Auditor`** is the only change.
- [Bitcraft.Application/Common/Security/AuthorizationPolicies.cs](../../../src/Bitcraft.Application/Common/Security/AuthorizationPolicies.cs) defines `ViewerOrAbove`. **Mechanical rename to `AuditorOrAbove`**.
- [Bitcraft.Infrastructure/DependencyInjection.cs](../../../src/Bitcraft.Infrastructure/DependencyInjection.cs) registers the `ViewerOrAbove` policy; the rename propagates here and to every `[Authorize(Policy = "ViewerOrAbove")]` site. The compiler will surface every stale reference.
- **No conflict.** D24's `IPolicyEvaluator` is additive — it sits beside the existing ASP.NET Core policy registrations, not replacing them in step 1. Controllers continue to use `[Authorize(Policy = "AuditorOrAbove")]` for now; migration to evaluator-driven authorization happens in WS6/WS7.

### 6.3 Existing migrations — no collision, but ordering note

Verified existing migrations in [src/Bitcraft.Infrastructure/Persistence/Migrations/](../../../src/Bitcraft.Infrastructure/Persistence/Migrations/):

1. `20260407180202_InitialCreate`
2. `20260421142543_AddIdentity`
3. `20260429091723_RealignAndAudit`
4. `20260429201016_AddBenchmarking`
5. `20260429210250_HardenBenchmarkingProvenance`

None use names that collide with the 8-step D25 sequence. **Step 1 will be migration #6** by timestamp ordering. The roles vocabulary rename will likely require a manual `migrationBuilder.Sql(...)` block (renaming a data row, not a schema column); EF will not auto-emit this — flagged in [§3.6](#36-migration).

### 6.4 Hardcoded credentials — WS1 carryover

[IdentityRoleSeeder.cs](../../../src/Bitcraft.Infrastructure/Identity/IdentityRoleSeeder.cs) lines 28–43 contain `admin@bitcraft.com` / `Admin1234!`. This is an Autopilot-Locked-adjacent surface (credentials in source) and **must be removed in step 1's PR**. Not optional. The full WS1 `BootstrapOptions` template is the §5.8 ambiguity.

### 6.5 No CI today

[.github/workflows/](../../workflows/) does not exist. Verified. Surfaced as §5.5 ambiguity. Risk: 8 unreviewed-by-CI step PRs accumulate on `feature-freeze` if no CI hook is added.

### 6.6 No `Directory.Build.props` today

Verified. The D26 file does not exist anywhere. Surfaced as §4.1 draft and §5.3 ambiguity. Not a conflict, just confirmation that introducing it is a greenfield addition.

### 6.7 No `.github/governance/abac-policies.md` today

Verified. The file does not exist. Per user instruction, not created here — only drafted at the content level in [§4.2](#42-sec-ops-abac-policy-catalog-paper-artifact). No conflict.

### 6.8 `tutor-studio/node_modules` absent

Not directly a step 1 concern (frontend work is WS9), but flagged so the Human plans `npm ci` ahead of the WS9 branch.

## 7. Lead Dev Recommendation: Start NET Dev on the working tree before Human picks branching?

**Recommendation: No. Wait for the Human to pick a branching strategy first.**

Defense:

- Step 1 is small (a rename plus three new interface/record files plus a DI registration plus a credential removal plus a manual-SQL migration). The Human's decision pass in §5 is shorter than the agent's implementation pass.
- Once code lives on the working tree but no branch is chosen, every minute increases the chance the Human accidentally commits onto the wrong branch. The hardcoded-credentials removal in [§6.4](#64-hardcoded-credentials--ws1-carryover) is sensitive enough that the diff should land on a named branch with a clean intent.
- The §5 ambiguities include three that affect step 1's PR shape directly: branch naming (§5.1), `Directory.Build.props` placement (§4.1 + §5.3), and the WS1 split (§5.8). Starting before they are answered creates rework risk in the very first PR.
- Counterpoint considered: agents could prepare a non-source-tree draft (e.g. the `Directory.Build.props` content and the new `Common/Authorization/` record contents) as text in this brief or in session notes, ready to paste once the branch exists. **That is acceptable** and is in fact what [§3](#3-ws2-step-1--identity-and-roles-vocabulary-file-touch-map) and [§4](#4-pre-development-items-doable-now-no-branch-required) already do. Net effect: agents are not idle; they are producing the kickoff artifact and the paper drafts.

If the Human disagrees and wants step 1 source edits on the working tree before picking a branch, NET Dev can begin with the **lowest-risk file**: the `Common/Authorization/` record + interface scaffolding (PolicyDecision.cs, Obligation.cs, IPolicyEvaluator.cs, PolicyRequest.cs). These are pure additive, no rename, no migration, no credential removal. Risk surface: near zero. **Conditional recommendation if Human overrides: start with those four files only; pause before touching `AppRoles.cs`, `AuthorizationPolicies.cs`, `IdentityRoleSeeder.cs`, or `DependencyInjection.cs`.**

---

## Output Summary For Planner

- **File created**: [`.github/plans/feature-freeze-2026-05-21/kickoff.md`](kickoff.md).
- **Approximate length**: ~360 lines / ~3,400 words.
- **Three most critical ambiguities surfaced** (in priority order for the Human's decision pass):
  1. Branching strategy + merge style (§5.1, §5.4). Gates every subsequent PR shape.
  2. `Directory.Build.props` placement (§5.3) — wrong placement silently fails to define `BITCRAFT_RUNTIME_LOCAL_ONLY` for tests projects, breaking D26's defense-in-depth.
  3. WS1 scope inside step 1 PR (§5.8) — credentials must come out in step 1; the rest is the Human's call.
- **Conflicts found in §6**: hardcoded credentials in `IdentityRoleSeeder` (§6.4) — must be addressed in step 1; manual-SQL migration needed for the role-row rename (§3.6); no CI on the repo today (§6.5). No conflicting domain shapes, no conflicting policy registrations, no conflicting migration names.
- **Recommendation on starting NET Dev before branching strategy is picked**: **No.** Wait. Conditional fallback: if Human overrides, restrict to the four additive `Common/Authorization/` files only. Defense in [§7](#7-lead-dev-recommendation-start-net-dev-on-the-working-tree-before-human-picks-branching).
