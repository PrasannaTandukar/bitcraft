# Bitcraft Copilot Instructions

Bitcraft is an Agentic AI Tutor management platform. Treat this repository as a .NET 10 Clean Architecture backend with EF Core, PostgreSQL, pgvector, Identity/JWT, MediatR/CQRS, and a React/Vite Tutor Studio client.

## Operating Model

- Humans and agents cooperate. Humans own product decisions, credentials, branch creation, commits, pushes, merges, releases, and final accountability.
- Use the departmental agent roster in `.github/agents/ROSTER.md`: Secretary, Researcher, Planner, Lead Dev, NET Dev, Web Dev, Dev Ops, Sec Ops, AI Ops, and Typewriter.
- Use `.github/inboxes/<agent-slug>/inbox.md` as the prioritized backlog per agent. Read your own inbox at the start of role-specific work, act on highest-priority items, and clean items you complete.
- Use `.github/agentic-ai/METRICS.md` for Change Impact Level, risk metrics, agent execution metrics, and model-routing reflection.
- Prefer small, validated iterations over broad speculative rewrites.
- Explain material design and algorithm decisions plainly, including tradeoffs and validation limits.

## Hard Boundaries

- Do not make unprompted architectural decisions or code changes.
- Do not perform Git write operations such as branch creation, commit, reset, checkout, merge, rebase, or push.
- Do not modify `.gitignore`, credentials, tokens, datasets, model weights, production secrets, or private local machine paths unless the user explicitly asks and the security impact is clear.
- Do not reference external packages from `src/Bitcraft.Application`.
- Agentic AI customization work belongs under `.github`; developer-facing project documentation belongs under `docs/`.
- Frontend work belongs in `tutor-studio/` and may be handled by Web Dev or Lead Dev when the user requests frontend work or a plan delegates it.
- Do not expose domain entities through API contracts.

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

## Workflow And Review

- Start with Secretary for work overview, Researcher for evidence discovery, or Planner for technical planning.
- Development responsibility flows through Lead Dev. Lead Dev delegates frontend work to Web Dev, backend work to NET Dev, infrastructure to Dev Ops, security to Sec Ops, and agentic stack work to AI Ops.
- Before review, classify Change Impact Level from `.github/agentic-ai/METRICS.md`. Higher impact changes need broader expert review and stronger validation.
- Audit work uses `.github/agentic-ai/AUDIT_TEMPLATES.md` and creates actionable inbox items rather than unbounded report sprawl.

## Validation

- For backend changes, run the narrowest relevant build or test first; broaden only when risk or touched surface requires it.
- For frontend changes, run the narrowest relevant lint, build, or browser validation available in `tutor-studio/`.
- For `.github` customization and docs changes, validate frontmatter, naming, markdown diagnostics, and internal consistency.
- Always report validation performed and anything intentionally not run.
