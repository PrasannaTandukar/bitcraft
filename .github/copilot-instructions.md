---
created_at: 2026-05-19
edited_at: 2026-09-21
creator: ai-ops
editor: ai-ops
ai_gen: true
---

# Bitcraft Copilot Instructions

Bitcraft is an Agentic AI Tutor management platform. Treat this repository as a .NET 10 Clean Architecture backend with EF Core, PostgreSQL, pgvector, Identity/JWT, MediatR/CQRS, and a React/Vite Tutor Studio client.

## Operating Model

- Humans and agents cooperate. Humans own product decisions, credentials, branch creation, commits, pushes, merges, releases, and final accountability.
- Use the departmental agent roster in `.github/agents/ROSTER.md`: Secretary, Planner, Lead Dev, Sec Ops, and AI Ops. Research, documentation, backend/frontend specialization, and release work live as skills invoked inside these agents.
- Use `.github/inboxes/<agent-slug>/inbox.md` as an optional local backlog per agent. Read it at the start of role-specific work when present. Do not treat it as tracked project state.
- Store durable plans, decisions, audits, research, reviews, and workflow rules in tracked files under `.github/plans/`, `.github/governance/`, `.github/audits/`, `.github/research/`, `.github/reviews/`, or `docs/`.
- Use `.github/governance/METRICS.md` for CSS, DIS, and Change Impact Level (internal routing signals only — no external tracking).
- Use the per-level protocol files in `.github/config/cil-<level>.yaml` to choose agents, research depth, allowed tools, validation, review, and human-escalation rules for any change.
- Persist active task plans under `.github/plans/<branch-slug>/` with `plan.yaml` and optional `plan.md`/`context.md`. Completed plans move to `.github/plans/archive/`. Audits, research, and reviews follow the same pattern under `.github/audits/`, `.github/research/`, and `.github/reviews/`. MCP excludes `archive/` from default context.
- Prefer small, validated iterations over broad speculative rewrites.
- Explain material design and algorithm decisions plainly, including tradeoffs and validation limits.

## Markdown Metadata And Documentation Access

- Every tracked Markdown file uses frontmatter fields `created_at`, `edited_at`, `creator`, `editor`, and `ai_gen`.
- `ai_gen: true` means agents may generate or substantially edit the document. `ai_gen: false` means agents need explicit human permission before generating or substantially rewriting it.
- Agents may update `edited_at` and `editor` when they edit a document, but only humans may change `ai_gen` after creation.
- Agent-created Markdown defaults to `ai_gen: true`; human-created Markdown defaults to `ai_gen: false`.
- Markdown files under `.github/governance/` are semi-locked report and doctrine files (AI-modifiable per `ai_gen`). Human-managed configuration lists (allow-lists, source rosters, similar typed data) are stored as **YAML** files alongside them, never as Markdown. First application: `.github/config/approved-external-sources.yaml`. See `HUMAN_DECISIONS.md` D-8.
- Developer-local documentation stacks live under ignored `docs/dev/<stack-slug>/` folders. Humans provide source files there; repository docs describe the convention but do not track those local stacks.
- Documentation lookup is local-first: consult repository docs and `docs/dev/` first, use approved external documentation only when the human explicitly grants it, use broader web retrieval only for deep research, then ask the human when sources remain missing or conflicting.
- Product Documents/Knowledge ingestion is out of scope for the agentic development documentation stack.

## Hard Boundaries

- Do not make unprompted architectural decisions or code changes.
- Do not perform Git write operations such as branch creation, commit, reset, checkout, merge, rebase, or push.
- Do not modify `.gitignore`, credentials, tokens, datasets, model weights, production secrets, or private local machine paths unless the user explicitly asks and the security impact is clear.
- Do not reference external packages from `src/Bitcraft.Application`.
- Agentic AI customization work belongs under `.github`; developer-facing project documentation belongs under `docs/`.
- Frontend work belongs in `tutor-studio/` and is handled by Lead Dev when the user requests frontend work or a plan includes it.
- Do not expose domain entities through API contracts.

## Autopilot-Locked Actions

Even when running in autopilot or batch mode, agents must HALT and request human input before any of the following. Tool availability does not imply authority.

- Git writes of any kind: branch creation, commit, push, merge, rebase, reset, tag, force operations, history rewrites.
- Editing `.gitignore`, `.copilotignore`, or any ignore file.
- Touching secrets, tokens, credentials, environment files, datasets, model weights, or production endpoints.
- Changing `ai_gen: false` in any frontmatter (humans only).
- Any operation classified `cil: critical` in the active `plan.yaml` (see `.github/config/cil-critical.yaml`).
- Database destructive operations (drop, truncate, schema-incompatible migration apply against shared environments).
- Enabling, installing, or invoking a write-capable MCP server or write-capable MCP tool.
- Modifying `.vscode/mcp.json` to add a server, expand allowed tools, or change scope.
- Deployment, release, or any operation that touches a non-local environment.
- Restoring a document from `.github/**/archive/` back to an active folder.

## Clean Architecture Rules

- Dependency flow: `Bitcraft.Api` -> `Bitcraft.Application` + `Bitcraft.Infrastructure`; `Bitcraft.Infrastructure` -> `Bitcraft.Application` + `Bitcraft.Domain`; `Bitcraft.Application` -> `Bitcraft.Domain`.
- `Bitcraft.Domain` and `Bitcraft.Application` must not reference `Bitcraft.Infrastructure` or `Bitcraft.Api`.
- CQRS requests, responses, DTOs, and API contracts are immutable records unless a framework requires otherwise.
- API contracts live in `src/Bitcraft.Api/Contracts`; Application interfaces live in `src/Bitcraft.Application/Common/Interfaces`.
- Controllers are organized by bounded context but routes must remain resource-shaped REST endpoints.

## Domain Rules

- Documents are source-of-truth artifacts in `Bitcraft.Domain.Documents`; Documents CRUD does not create embeddings.
- Knowledge is the vectorized RAG/MCP layer derived from Documents. Re-embedding clears Knowledge entries for the document and reruns vectorization.
- Tutor is the aggregate root for prompt versions, model connection, knowledge links, tool definitions, deployment profile, and configuration snapshots.
- Admin-managed model resources are `ModelCatalogEntry`; editor-managed per-tutor selection is `TutorModelConnection`.
- Aggregates that need benchmarking, replay, rollback, or version history use immutable append-only `XSnapshot` records with `CapturedById` linked to `AppUser`.
- Persisted aggregates inherit `BaseModel`; owner/creator/modifier references use Guid foreign keys plus EF navigation properties.
- Deletion is hard by default. Do not introduce soft-delete without an explicit feature need.

## Coding Standards

- Use C# 12+ style, primary constructors where appropriate, file-scoped namespaces, early returns, async I/O, and cancellation tokens.
- Use the Application `Result<T>` pattern for expected failures; translate to HTTP in Api.
- Name interfaces with `I`, async methods with `Async`, DTOs/contracts with `Request` or `Response`, snapshots with `Snapshot`, links with `Link`, and tutor-scoped resources with `Tutor`.
- Keep aggregate-local helper enums, helper records/classes, and supporting subtypes in the primary aggregate file unless reused across aggregates or bounded contexts.
- Use `NotImplementedException("Implementation pending for this capability.")` only for intentionally deferred strongly typed seams.

## Writing Style

- Write agent-facing text (inbox tasks, acceptance criteria, instructions, prompts, skills, error messages, tool descriptions) in Simplified Technical English per `.github/skills/bitcraft-ste/SKILL.md`: active voice, one instruction per sentence, ≤20-word instructions, no semicolons, no phrasal verbs, no hedge stacking, no synonym rotation.
- Default modes: Strict for anything an agent executes against; STE-flavored for docs and narrative prose.
- Never upgrade a hedge to a fact or drop a condition to shorten a sentence.

## Workflow And Review

- The workflow has three phases with entry skills: `bitcraft-plan` (Planning and Research), `bitcraft-develop` (Development), and `bitcraft-review` (Review). Each phase ends by emitting a ready-to-paste prompt for the next phase. See `.github/governance/DEVELOPER_GUIDE.md` Standard Flow.
- Start with Secretary for work overview, the `bitcraft-research` skill for evidence discovery, or Planner for technical planning.
- Development responsibility sits with Lead Dev (backend and frontend). Sec Ops owns integrity (security, privacy, CI/CD, deployment). AI Ops owns the agentic stack. Secretary owns coordination and documentation.
- Before review, classify Change Impact Level from `.github/governance/METRICS.md`. Higher impact changes need broader expert review and stronger validation.
- Audit work uses `.github/audits/templates/audit.template.md` and creates actionable inbox items rather than unbounded report sprawl.

## Validation

- For backend changes, run the narrowest relevant build or test first; broaden only when risk or touched surface requires it.
- For frontend changes, run the narrowest relevant lint, build, or browser validation available in `tutor-studio/`.
- For `.github` customization and docs changes, validate frontmatter, naming, markdown diagnostics, and internal consistency.
- Always report validation performed and anything intentionally not run.
