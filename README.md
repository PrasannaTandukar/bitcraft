---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: human
editor: secretary
ai_gen: false
---
# Bitcraft

Agentic AI tutor management platform with source documents, knowledge retrieval, benchmarking, and RAG capabilities.

## Tech Stack

- **Backend:** ASP.NET Core (.NET 10), Entity Framework Core, PostgreSQL + pgvector
- **Tutor Studio:** React + Vite frontend for tutor configuration and management
- **Benchmarking:** Python sidecar (FastAPI + DeepEval) for benchmark execution
- **AI:** Ollama (host-based or containerized)
- **Architecture:** Clean Architecture (screaming, feature-based)
- **Testing:** xUnit unit tests plus API, infrastructure, and sidecar integration tests

## Requirements

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0+ | [Download](https://dotnet.microsoft.com/download) |
| Docker Desktop / Docker Engine | Latest | Compose plugin required for local containers |
| Node.js | 20+ | Optional for local Tutor Studio development |
| Python | 3.12+ | Optional only if running the benchmark sidecar outside Docker |

## Configuration

Copy `.env.example` to `.env` and edit environment variables as needed. The stack has sensible defaults when variables are unset, but an explicit `.env` is recommended for repeatable local and deployment setups.

```bash
cp .env.example .env
```

Key variables:
- `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` — PostgreSQL credentials
- `OLLAMA_BASE_URL` — Ollama service endpoint. Default assumes Ollama running on the host at `http://host.docker.internal:11434`.
- `OLLAMA_EMBED_MODEL` — Embedding model to use. Default: `qwen3-embedding:0.6b`.
- `OLLAMA_DOCKER_TAG` — Docker tag used by the optional Ollama container override.

## Build and Test

```bash
dotnet tool restore
dotnet build bitcraft.slnx
dotnet test bitcraft.slnx
```

The solution test command covers the .NET test projects in `bitcraft.slnx`. The FastAPI sidecar integration test runs separately:

```bash
python -m pip install -r tests/Bitcraft.SidecarIntegration/requirements.txt
python -m pytest tests/Bitcraft.SidecarIntegration/test_sidecar_api.py
```

Optional Tutor Studio build from the host:

```bash
cd tutor-studio
npm ci
npm run build
```

## Container Deployment Modes

The full cross-platform setup guide (Windows + Docker Desktop, macOS Intel/Apple Silicon, Linux, Linux + NVIDIA GPU, NVIDIA Jetson Orin) lives in [docs/local-setup.md](docs/local-setup.md). Quickstart:

```bash
cp .env.example .env
task up:dev          # Core + tutor-studio HMR on http://localhost:5173
# or
./run.sh             # interactive menu (Linux/macOS/WSL)
./run.ps1            # interactive menu (Windows PowerShell)
```

Without [Task](https://taskfile.dev/installation/) installed, see the raw `docker compose` recipes in [docs/local-setup.md](docs/local-setup.md#raw-compose-commands).

## Local Development

### Backend-only development

```bash
# Start local backing services
docker compose up db benchmark-sidecar -d

# Restore tools (EF Core CLI)
dotnet tool restore

# Apply migrations
dotnet ef database update --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api

# Run the API
dotnet run --project src/Bitcraft.Api
```

The API will be available at `http://localhost:5260` and `https://localhost:7123` when started through the local launch profile. The containerized API remains available at `http://localhost:8000`.

If you use benchmarking endpoints locally, keep `benchmark-sidecar` running. If you use vectorization, ensure Ollama is reachable through `OLLAMA_BASE_URL`.

### Tutor Studio local development

```bash
cd tutor-studio
npm ci
npm run dev
```

The Vite dev server is available at `http://localhost:5173`.

**Database connection string (local client tools):**
```
Host=localhost;Port=5433;Database=bitcraft;Username=bitcraft_user;Password=bitcraft_password
```

### EF Core Migration Cheatsheet

```bash
# Add a new migration
dotnet ef migrations add <MigrationName> --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api

# Apply pending migrations
dotnet ef database update --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api

# Remove last migration (if not applied)
dotnet ef migrations remove --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api
```

## API Surface

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/api/auth/register` | Register a Tutor Studio user |
| `POST` | `/api/auth/login` | Authenticate and receive a JWT with role claims |
| `GET` | `/api/admin/roles` | List seeded authorization roles |
| `GET` | `/api/admin/users` | List users with their role memberships |
| `GET/POST` | `/api/admin/users/{userId}/roles` | View and assign user roles |
| `DELETE` | `/api/admin/users/{userId}/roles/{role}` | Revoke a specific user role |
| `GET/POST` | `/api/admin/model-catalog` | Manage Admin-owned model catalog entries |
| `GET` | `/api/admin/model-catalog/{id}/health` | Check model reachability and availability |
| `GET/POST/PUT/DELETE` | `/api/documents` | Manage source documents owned by the current user |
| `POST` | `/api/documents/{id}/vectorize` | Rebuild knowledge chunks for an owned source document |
| `POST` | `/api/knowledge/search` | Search tutor-scoped knowledge chunks |
| `GET/POST/PUT` | `/api/tutors` | Manage owned tutors |
| `Multiple` | `/api/tutors/{tutorId}/prompts`, `/tools`, `/knowledge-sources`, `/model-connection`, `/deployment-profile`, `/snapshots` | Manage tutor sub-resources |
| `GET/POST/DELETE` | `/api/benchmark/templates` | Manage benchmark templates |
| `GET/POST` | `/api/tutors/{tutorId}/benchmark-runs` | List and queue tutor benchmark runs |
| `GET` | `/api/tutors/{tutorId}/benchmark-runs/{runId}` | Inspect benchmark run detail |
| `GET` | `/api/tutors/{tutorId}/benchmark-runs/{runId}/progress` | Poll lightweight benchmark progress |
| `POST` | `/api/ollama/generate` | Generate text using the configured local Ollama chat model |

See Swagger UI (`/swagger`) for full request/response schemas. Additional operational notes live in `docs/authorization-roles.md` and `docs/benchmarking-templates.md`.

## VS Code Extensions

Open this project in VS Code and accept the workspace extension recommendations, or install manually:

**Required**

| Extension | ID | Purpose |
|---|---|---|
| C# Dev Kit | `ms-dotnettools.csdevkit` | IntelliSense, build, debug, solution explorer |
| C# | `ms-dotnettools.csharp` | Language engine (required by Dev Kit) |
| .NET Install Tool | `ms-dotnettools.vscode-dotnet-runtime` | Runtime support for Dev Kit |
| Docker | `ms-azuretools.vscode-docker` | Compose management, container logs |

**Recommended**

| Extension | ID | Purpose |
|---|---|---|
| REST Client | `humao.rest-client` | Run `.http` files directly in editor |
| PostgreSQL | `ms-ossdata.vscode-postgresql` | Browse DB, run ad-hoc queries |
| YAML | `redhat.vscode-yaml` | docker-compose syntax + validation |
| Error Lens | `usernamehw.errorlens` | Inline diagnostics on same line |
| EditorConfig | `editorconfig.editorconfig` | Consistent formatting across team |

**Optional**

| Extension | ID | Purpose |
|---|---|---|
| GitLens | `eamodio.gitlens` | Enhanced git blame and history |
| DotENV | `mikestead.dotenv` | `.env` file syntax highlighting |

## Agentic Development Workflow

Bitcraft also includes a repo-local agentic workflow layer under `.github/` and a minimal read-only MCP server under `mcp-server/`.

- Human-facing overview: `docs/agentic-ai.md`
- Governance and operating rules: `.github/governance/README.md`
- MCP server scope and setup: `mcp-server/README.md`

## Branch Convention

The active plan slug is derived directly from the current working Git branch name. Agents do not create branches.

If the working branch is `dev`, `main`, or `test`, do not create or assume a matching plan folder. Use a human-created feature branch or treat the change as a small in-place edit.

When a branch is acceptable, the plan folder slug is the branch name lowercased with `/` replaced by `-`. Example: `feature/tutor-snapshots` becomes `.github/plans/feature-tutor-snapshots/`.

## Licensing and Transparency

This repository does not yet include a top-level `LICENSE` file.

- Recommended default: `Apache-2.0` for permissive open-source distribution with an explicit patent grant.
- Simpler alternative: `MIT` if minimal license text is preferred.
- Stronger reciprocity option: `MPL-2.0` if file-level copyleft is desired.

See `docs/transparency-and-privacy.md` for the current open-source posture, privacy statement, third-party disclosures, and exception register.

## Project Structure

```
.github/                       # Agentic workflow, governance, prompts, inboxes, config
db/                            # PostgreSQL + pgvector image build context
docs/                          # Technical notes, role matrix, transparency statement
mcp-server/                    # Read-only MCP server for governance and repo context
sidecar/                       # Benchmark sidecar (FastAPI + DeepEval)
src/
├── Bitcraft.Api/              # HTTP entry point, controllers, composition root
├── Bitcraft.Application/      # Use cases organized by business capability
├── Bitcraft.Domain/           # Core business concepts and rules
└── Bitcraft.Infrastructure/   # EF Core, pgvector, persistence and integrations
tests/
├── Bitcraft.UnitTests/
├── Bitcraft.SidecarIntegration/
├── Bitcraft.InfrastructureIntegrationTests/
└── Bitcraft.ApiIntegrationTests/
tutor-studio/                  # React + Vite frontend
```

### Current capability grouping

- **Documents** — source-of-truth source artifacts owned by users
- **Knowledge** — vectorized retrieval derived from documents and scoped through tutors
- **Tutoring** — tutor configuration, prompts, tools, model connections, knowledge links, deployment profiles, snapshots, and benchmark runs
- **Benchmarking** — reusable benchmark templates and asynchronous tutor benchmark runs executed through the sidecar
- **Admin** — model catalog management and Admin/Editor/Auditor role management
- **Infrastructure/Persistence** — database mappings, repository implementations, migrations, pgvector, Identity, Ollama adapters, and benchmark sidecar integration
- **Api** — thin controllers using request/response contracts, Swagger metadata, JWT auth, and role policies

**Dependency direction:** Api → Application + Infrastructure, Infrastructure → Application + Domain, Application → Domain. Core (Domain + Application) never references Infrastructure or Api.
