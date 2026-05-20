---
created_at: 2026-05-20
edited_at: 2026-05-20
creator: ai-ops
editor: ai-ops
ai_gen: true
---
# Bitcraft Twelve-Factor Compliance Map

Per [feature-freeze-2026-05-21 D0b](../plans/feature-freeze-2026-05-21/plan.decisions.md#d0b--foundational-deployment-vision), the [Twelve-Factor App](https://12factor.net/) is Bitcraft's foundational deployment vision. This document maps each factor to a Bitcraft-specific status (Compliant / Partial / Gap), cites repository evidence, and records the next action where one is needed.

This is a living document. AI Ops updates it whenever a locked decision, infrastructure change, or governance update materially affects a factor.

## I. Codebase — Compliant

One codebase tracked in version control, many deploys. The Bitcraft monorepo (`src/`, `tutor-studio/`, `sidecar/`, `mcp-server/`, `.github/`) is a single Git repository. Branches are humans-only ([HUMAN_DECISIONS.md D-3](HUMAN_DECISIONS.md)).

- Evidence: [bitcraft.slnx](../../bitcraft.slnx), [BRANCH_CONVENTION.md](BRANCH_CONVENTION.md).
- Next action: none.

## II. Dependencies — Compliant

Explicitly declared and isolated dependencies. .NET dependencies are pinned in `*.csproj` files; frontend in [package.json](../../tutor-studio/package.json); Python sidecar and MCP server in `requirements.txt`. No system-package assumptions beyond Docker images.

- Evidence: [src/Bitcraft.Api/Bitcraft.Api.csproj](../../src/Bitcraft.Api/Bitcraft.Api.csproj), [tutor-studio/package.json](../../tutor-studio/package.json), [sidecar/requirements.txt](../../sidecar/requirements.txt), [mcp-server/requirements.txt](../../mcp-server/requirements.txt).
- Next action: none.

## III. Config — Partial

Strict separation of config from code. PoC currently mixes static `appsettings.json` defaults with environment-variable overrides. [Feature-freeze D9](../plans/feature-freeze-2026-05-21/plan.decisions.md#d9--secrets-and-configuration) locks env-vars-first for production-bound deployments; tracked `template.<file>.<ext>` is reserved for cases where a sample is genuinely required, not a routine bootstrap requirement.

- Evidence: [src/Bitcraft.Api/appsettings.json](../../src/Bitcraft.Api/appsettings.json), feature-freeze plan WS2, [D9](../plans/feature-freeze-2026-05-21/plan.decisions.md#d9--secrets-and-configuration).
- Next action: WS2 implements `BootstrapOptions` with `IOptions<T>` fail-fast binding; Dev Ops to gate CI on env-vars-first per the new dev-ops inbox seed.

## IV. Backing Services — Partial

Treat backing services as attached resources. PostgreSQL + pgvector, Ollama, and the FastAPI sidecar are addressed via connection strings/endpoints today; runtime-engine connectivity is being formalized into a first-class [`RuntimeEngine`](../plans/feature-freeze-2026-05-21/plan.decisions.md#d11--runtimeengine-entity) entity in this freeze (D11). Local file storage for documents is in-process, which is acceptable for the PoC scope.

- Evidence: [docker-compose.yml](../../docker-compose.yml), [docker-compose.ai.yml](../../docker-compose.ai.yml), [Program.cs](../../src/Bitcraft.Api/Program.cs).
- Next action: complete WS2/WS3 RuntimeEngine landing; revisit object storage when binary document ingestion is in scope (post-freeze).

## V. Build, Release, Run — Partial

Strict separation between build, release, and run stages. .NET `dotnet build` and Docker image builds are clean build stages; release-stage configuration injection is still ad-hoc on local-developer flows. [D7](../plans/feature-freeze-2026-05-21/plan.decisions.md#d7--activation-vs-deployment) preserves `DeploymentProfile` for future export/release configuration aligned with this factor.

- Evidence: [src/Bitcraft.Api/Dockerfile](../../src/Bitcraft.Api/Dockerfile), [tutor-studio/Dockerfile.prod](../../tutor-studio/Dockerfile.prod), [docker-compose.prod.yml](../../docker-compose.prod.yml).
- Next action: Dev Ops formalizes a release-stage configuration injection contract post-freeze; the `BITCRAFT_RUNTIME_LOCAL_ONLY` MSBuild constant ([D8](../plans/feature-freeze-2026-05-21/plan.decisions.md#d8--runtime-api-authentication)) is a build-stage decision that must remain off in CI Release builds.

## VI. Processes — Compliant

Execute the app as one or more stateless processes. API and sidecar processes are stateless; persistent state lives in PostgreSQL/pgvector. Sessions are JWT-based (no server-side session store).

- Evidence: [Program.cs](../../src/Bitcraft.Api/Program.cs), [sidecar/app/main.py](../../sidecar/app/main.py).
- Next action: none.

## VII. Port Binding — Compliant

Export services via port binding. ASP.NET Core binds via Kestrel; Vite dev server, FastAPI sidecar, and MCP server (stdio) all self-host. No web-server appliance assumed.

- Evidence: [src/Bitcraft.Api/Properties/launchSettings.json](../../src/Bitcraft.Api/Properties), [tutor-studio/vite.config.js](../../tutor-studio/vite.config.js).
- Next action: none.

## VIII. Concurrency — Partial

Scale out via the process model. Backend handlers are async with cancellation tokens; horizontal scaling is feasible but unvalidated. Knowledge vectorization is currently in-process and would need a worker boundary for high-volume ingestion.

- Evidence: backend handlers under [src/Bitcraft.Application/Features](../../src/Bitcraft.Application/Features).
- Next action: defer worker-boundary extraction until post-freeze; not blocking the PoC.

## IX. Disposability — Partial

Fast startup and graceful shutdown. ASP.NET Core handles SIGTERM correctly; long-running operations (vectorization batches) lack explicit cancellation/idempotency guarantees end-to-end. The [`SaveChanges` interceptor audit path](../plans/feature-freeze-2026-05-21/plan.decisions.md#d10--auditing-poc) (D10) is fast and disposable.

- Evidence: [Program.cs](../../src/Bitcraft.Api/Program.cs), feature-freeze plan WS5.
- Next action: ensure vectorization commands are idempotent on retry (already partially specified in WS5 `RebuildKnowledgeCommand`); revisit if a queue is introduced.

## X. Dev/Prod Parity — Partial

Keep development, staging, and production as similar as possible. Local dev uses Docker Compose with the same Postgres/pgvector image used downstream. Frontend dev server differs from the production Nginx build; this is acceptable for PoC.

- Evidence: [docker-compose.dev.yml](../../docker-compose.dev.yml), [docker-compose.prod.yml](../../docker-compose.prod.yml), [tutor-studio/nginx.conf](../../tutor-studio/nginx.conf).
- Next action: none for freeze.

## XI. Logs — Gap

Treat logs as event streams. Backend uses default ASP.NET Core logging to stdout (good), but there is no structured-log contract, correlation-id propagation, or aggregation target documented yet. Sidecar and MCP server have minimal logging.

- Evidence: [Program.cs](../../src/Bitcraft.Api/Program.cs).
- Next action: post-freeze — Dev Ops + AI Ops define a structured logging contract (Serilog or built-in `ILogger` with JSON formatter) and a correlation-id middleware. Not blocking the PoC freeze.

## XII. Admin Processes — Partial

Run admin/management tasks as one-off processes. EF Core migrations are run via the `ef-add-migration` / `ef-update-database` VS Code tasks, which is correct one-off-process behavior. Identity bootstrap is moving from in-process startup seeding to a configuration-driven idempotent path in WS1.

- Evidence: [.vscode/tasks.json](../../.vscode), [src/Bitcraft.Infrastructure/Identity/IdentityRoleSeeder.cs](../../src/Bitcraft.Infrastructure/Identity/IdentityRoleSeeder.cs), feature-freeze plan WS1.
- Next action: complete WS1 bootstrap-options work; document admin operations in [docs/architecture.md](../../docs/architecture.md) once that artifact lands.

## Cross-References

- [HUMAN_DECISIONS.md D-9](HUMAN_DECISIONS.md#d-9--twelve-factor-compliance-map) — pointer to this document.
- [plan.decisions.md Companion Artifacts](../plans/feature-freeze-2026-05-21/plan.decisions.md#companion-artifacts) — registers this document.
- [Twelve-Factor App](https://12factor.net/) — canonical reference.
