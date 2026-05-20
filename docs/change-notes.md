---
created_at: 2026-05-20
edited_at: 2026-05-20
creator: planner
editor: secretary
ai_gen: true
---
# Bitcraft Change Notes

> **Owner**: Secretary (writes), Lead Dev (reviews), Planner (links from active plan).
> **Cadence**: one entry per merged batch (release, freeze, or significant single-PR change).
> **Audience**: developers and team members. Concise, referenced, learner-friendly.
> **Sister doc**: [`docs/architecture.md`](architecture.md) for the architectural narrative; locked decisions live in [`.github/plans/<plan-slug>/plan.decisions.md`](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md).

> **Status**: scaffold — first real entry will be the feature-freeze 2026-05-21 batch, drafted by Secretary once the freeze closes.

## Format

Each batch entry follows this template:

```markdown
## YYYY-MM-DD — Batch Title

- **Plan**: link to the originating `plan.md`.
- **Decisions**: links to specific `D#` rows in `plan.decisions.md`.
- **What changed (developer-facing)**: 1–5 bullets.
- **Why it changed**: short rationale.
- **What you must do differently**: migration / habit / convention impact.
- **Where to read more**: arch sections, ADRs, governance docs.
```

## Entries

### 2026-05-20 — Feature Freeze v3.1 Planning Pass (pre-development)

This entry documents the planning batch that locked the v3.1 decision set for the upcoming feature freeze. **No source code has shipped yet** — development against these decisions begins next. The entry exists so that when implementation lands, the rationale and the "what you must do differently" rules are already in the developer-facing log.

- **Plan**: [feature-freeze-2026-05-21/plan.md](../.github/plans/feature-freeze-2026-05-21/plan.md) (v3.1), companion artifacts [plan.decisions.md](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md), [plan.erm.md](../.github/plans/feature-freeze-2026-05-21/plan.erm.md), [plan.frontend.md](../.github/plans/feature-freeze-2026-05-21/plan.frontend.md).
- **Decisions**:
  - Foundations: [D0a tenancy](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d0a--tenancy-model), [D0b twelve-factor](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d0b--foundational-deployment-vision).
  - Authorization and identity: [D1 ABAC](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d1--authorization-model), [D1.a Document.Visibility](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d1a--first-abac-resource-attribute-documentvisibility), [D2 TutorMember ownership](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d2--resource-ownership-pattern), [D8 build-time runtime auth](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d8--runtime-api-authentication), [D9 config and secrets](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d9--secrets-and-configuration), [D10 auditing](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d10--auditing-poc).
  - Models and runtime: [D3 Model TPH](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d3--model-registry-shape), [D11 RuntimeEngine](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d11--runtimeengine-entity), [D11.a provider enum surface](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d11a--runtimeengineprovidertype-enum-surface).
  - Documents, Knowledge, retrieval: [D4 document scoping](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d4--document-scoping), [D4b runtime semantics](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d4b--documentknowledge-runtime-semantics), [D5 embedding + ANN](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d5--embedding-dimensions-and-ann-index), [D5.a HNSW default](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d5a--default-ann-strategy-selection), [D12 no DocumentFolderMember](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d12--documentfoldermember).
  - Tutor revisions, activation, runtime contract: [D6 revisions + snapshots](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d6--tutor-versioning), [D7 activation vs deployment](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d7--activation-vs-deployment), [D13 tool overrides deferred](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d13--tutorrevisiontool-override), [D22 activation slug](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d22--activation-slug), [D22.a slug on activation](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d22a--activation-slug-storage-location), [D23 effective document set](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d23--effective-document-set).
  - Frontend stack: [D14–D20](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d14d20--frontend-stack), [D18.a TS migration order](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d18a--typescript-migration-order).
  - Migrations: [D21 migration sequencing](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d21--migration-sequencing).
- **What changed (developer-facing)**:
  - `RuntimeEngine` becomes a real first-class entity (`Id`, `Name`, `ProviderType`, `Endpoint`, `ApiKeySecretRef`, `IsEnabled`) and `Model.RuntimeEngineId` replaces provider/endpoint columns on `Model` (D11, D3). `Model` is EF Core TPH with concrete leaves `InteractionModel` / `EmbeddingModel` and reserved `JudgeModel` / `RerankerModel`.
  - Runtime API anonymous mode is now a **build-time** MSBuild constant `BITCRAFT_RUNTIME_LOCAL_ONLY` — non-local builds physically exclude the anonymous controller variant. There is no runtime flag (D8).
  - Authorization is **ABAC throughout** via a centralized policy evaluator in the Application layer; `Admin` / `Editor` / `Auditor` are coarse subject attributes only, and `Document.Visibility` is the first resource attribute on a domain entity (D1, D1.a).
  - `KnowledgeChunk.Embedding` stays untyped pgvector with a per-`EmbeddingModelId` `vector_dims` CHECK; ANN index is configurable per deployment, **HNSW by default** with IVFFlat as the supported alternative, managed by the `EnableEmbeddingModelCommand` / `DisableEmbeddingModelCommand` pair (D5, D5.a).
  - `Document` is tutor-agnostic; `TutorDocument` is the M:N attachment; `DocumentFolder` is **admin-only** and never visible at Tutor runtime — only `Knowledge` is consumed at runtime (D4, D4b).
  - Tutor Studio migrates to TypeScript **now**, in order: Settings → ModelCatalog → Tutors → Files → Benchmarking; shared modules migrate after three pages are converted; `strict` and `noUncheckedIndexedAccess` flip on at the end (D18, D18.a).
  - Schema evolution is an 8-step dependency-ordered migration sequence (Identity vocabulary → resource membership → RuntimeEngine → Model TPH → Tutor↔Document M:N → dynamic-dimension Knowledge → Tutor revisions → benchmark FK realignment); humans run `dotnet ef migrations add ...` per [AGENTS.md](../AGENTS.md) (D21, [plan.erm.md §10](../.github/plans/feature-freeze-2026-05-21/plan.erm.md#10-migration-sequencing)).
- **Why it changed**: v3.1 adopts the [Twelve-Factor App](https://12factor.net/) as the shared deployment doctrine (D0b) so that Config, Build/Release/Run, Backing services, and Disposability stop being implicit assumptions. It pivots authorization to ABAC (D1) so multi-owner Tutors, draft/active revisions, and admin-vs-member document operations are decided through one auditable evaluator rather than scattered role checks. Together these two shifts let the freeze fix runtime trust at compile time (D8), separate source-of-truth Documents from the runtime-visible Knowledge index (D4b), and decouple engine identity from model rows (D11).
- **What you must do differently**:
  - **Never expose `Document` (or any Domain entity) through API contracts.** Map to a contract record in [src/Bitcraft.Api/Contracts](../src/Bitcraft.Api/Contracts) — Domain stays inside the backend.
  - **Use the centralized policy evaluator (`IPolicyEvaluator`) for every authorization decision.** Controllers may shape requests and translate `Result<T>` to HTTP, but they must not branch on resource attributes like `TutorMember.ResourceRole`, `TutorRevision.Status`, or `Document.Visibility` themselves.
  - **Do not put provider, endpoint, or API-key columns on `Model`.** That data belongs on `RuntimeEngine`; `Model` carries `RuntimeEngineId`.
  - **Do not hardcode an embedding dimension on the `vector` column.** Use the per-`EmbeddingModelId` partial index path and let the `vector_dims` CHECK enforce shape.
  - **Treat `DocumentFolder` as an admin-only organization tool.** It never reaches Tutor runtime, never appears in retrieval scope, and is not a permission boundary.
  - **Do not perform Git writes or run EF migrations as an agent.** Humans run `dotnet ef migrations add ...` and own commits/pushes/merges per [AGENTS.md](../AGENTS.md).
- **Where to read more**:
  - [docs/architecture.md §1 Vision and Constraints](architecture.md#1-vision-and-constraints), [§2 System Overview](architecture.md#2-system-overview), [§9 Cross-Cutting Concerns](architecture.md#9-cross-cutting-concerns), [§10 Reference Knowledgebase](architecture.md#10-reference-knowledgebase).
  - [.github/governance/twelve-factor.md](../.github/governance/twelve-factor.md) — Bitcraft-specific 12-Factor compliance map.
  - [plan.decisions.md](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md) — full decision register with options considered and references.
  - [plan.erm.md §10 — Migration Sequencing](../.github/plans/feature-freeze-2026-05-21/plan.erm.md#10-migration-sequencing).

### 2026-05-20 — Feature Freeze v3.3 Closing Pass (procedural locks)

Same-day follow-up that closes the remaining Open Human Decisions and resolves the three discrepancies surfaced during planning. Still pre-development; no source code has shipped.

- **Plan**: [feature-freeze-2026-05-21/plan.md](../.github/plans/feature-freeze-2026-05-21/plan.md) (now v3.3); 33 locked decisions.
- **New locked decisions**:
  - [D22.b activation slug storage shape](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d22b--activation-slug-storage-shape) — nullable `ActivationSlug` and `PreviousActivationSlug` columns live on the `TutorRevision` row whose `Status` is `Active`. No separate `TutorActivation` entity.
  - [D24 ABAC policy evaluator location](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d24--centralized-abac-policy-evaluator-location) — `Bitcraft.Application/Common/Authorization/IPolicyEvaluator` returns `PolicyDecision { Effect, Obligations, Reasons }`; Infrastructure supplies attribute-fetching adapters under `Bitcraft.Infrastructure/Authorization/`.
  - [D25 backend 8-step migration sequence](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d25--backend-migration-sequence-8-steps) — binding order for WS2–WS7 (Identity-And-Roles-Vocabulary → Resource-Membership-Tables → RuntimeEngine-Entity → Model-Tph-Hierarchy → Tutor-Document-Mn → Knowledge-Dynamic-Dimensions → Tutor-Revisions → Benchmark-Fk-Realign).
  - [D26 `BITCRAFT_RUNTIME_LOCAL_ONLY` definition site](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d26--bitcraft_runtime_local_only-definition-site-and-ci-defaults) — defined in repo-root `Directory.Build.props` under a `Condition` guard for `Configuration == 'Local'`. Off in `Debug` / `Release`. CI never sets it.
  - [D27 OpenAPI client deferral](../.github/plans/feature-freeze-2026-05-21/plan.decisions.md#d27--frontend-openapi-client-adoption-timing) — frontend OpenAPI-generated client adoption deferred until at least one full PoC iteration of stable backend contracts; hand-written TS clients typed from the same Zod schemas (D15) until then.
- **What changed (developer-facing)**:
  - The active `TutorRevision` carries its own slug (and a one-revision redirect carry-over). When you activate a renamed revision, the prior slug remains resolvable for exactly one activation window before it is dropped.
  - Authorization decisions all go through `IPolicyEvaluator.EvaluateAsync(...)`; obligations (audit, redact) are first-class return data, not after-thoughts.
  - Migrations are scheduled, ordered, and **per-step builds-green** — do not bundle unrelated schema changes into a single migration. Each of the eight steps is one PR.
  - `BITCRAFT_RUNTIME_LOCAL_ONLY` is set **only** when MSBuild configuration equals `Local`. If you see it set in a Debug/Release build or a CI job, that is a defect.
  - Do not invest in OpenAPI client codegen for `tutor-studio/` yet; type your hand-written clients from the Zod schemas you already write for forms.
- **What you must do differently**:
  - When evolving `TutorRevision`, remember the slug columns are nullable and the partial uniqueness rule (`WHERE ActivationSlug IS NOT NULL`) is enforced at the database level; do not invent your own uniqueness check in code.
  - When adding a new authorization rule, register a policy that the evaluator can resolve — do not write ad hoc `if (user.Role == ...)` checks in handlers or controllers.
  - When opening a backend migration PR, name it after its step in D25 and confirm the prior step still builds clean against the new migration.
- **Where to read more**:
  - [plan.erm.md §7.2 TutorRevision](../.github/plans/feature-freeze-2026-05-21/plan.erm.md#72-tutorrevision-uniform-versioning) — updated with the two slug columns.
  - [plan.frontend.md §9 Gates](../.github/plans/feature-freeze-2026-05-21/plan.frontend.md#9-gates-before-this-iteration-starts) — only Open Decision **O4** (library set + admin-folder UX cohesion) remains; TypeScript-now itself is **not** gated.
  - [.github/governance/twelve-factor.md](../.github/governance/twelve-factor.md) Factors V/X for build-time runtime gating.

## Revision History

- 2026-05-20 — added v3.3 closing-pass entry covering D22.b, D24, D25, D26, D27 (Secretary follow-up).
- 2026-05-20 — first real entry: Feature Freeze v3.1 planning pass (Secretary).
- 2026-05-20 — scaffold introduced by Planner.
