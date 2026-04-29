# Bitcraft

Agentic AI platform with document embeddings, vector search, and RAG capabilities.

## Tech Stack

- **Backend:** ASP.NET Core (.NET 10), Entity Framework Core, PostgreSQL + pgvector
- **Tutor Studio:** React + Vite frontend for tutor configuration and management
- **AI:** Ollama (local inference)
- **Architecture:** Clean Architecture (screaming, feature-based)

## Getting Started with Development

### Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0+ | [Download](https://dotnet.microsoft.com/download) |
| Docker Desktop | Latest | Required for local infrastructure |
| Node.js | 20+ | Frontend development |

### Configuration

Copy `.env.example` to `.env` and edit environment variables as needed:

```bash
cp .env.example .env
```

Key variables:
- `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` — PostgreSQL credentials
- `OLLAMA_BASE_URL` — Ollama service endpoint (default: `http://host.docker.internal:11434` — assumes Ollama running on host)
- `OLLAMA_EMBED_MODEL` — Embedding model to use (default: `qwen3-embedding:0.6b`)

### Quick Start (Full Stack)

```bash
docker compose up
```

This starts:
- PostgreSQL + pgvector (port 5433 for local client access)
- Backend API (port 8000) with Swagger at `http://localhost:8000/swagger`
- Tutor Studio frontend (port 5173)
- Runs EF Core migrations automatically

**Note:** By default, this expects Ollama running on your host machine at `http://localhost:11434`. To run Ollama in Docker instead:

```bash
docker compose -f docker-compose.yml -f docker-compose.ai.yml up
```

For GPU acceleration:

```bash
docker compose -f docker-compose.yml -f docker-compose.ai.gpu.yml up
```

### Backend-Only Development

```bash
# Ensure DB is running
docker compose up db -d

# Restore tools (EF Core CLI)
dotnet tool restore

# Apply migrations
dotnet ef database update --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api

# Run the API
dotnet run --project src/Bitcraft.Api
```

The API will be available at `http://localhost:8000` with Swagger UI at `http://localhost:8000/swagger`.

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

### API Endpoints

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/api/auth/register` | Register a Tutor Studio user |
| `POST` | `/api/auth/login` | Authenticate and receive a JWT with role claims |
| `GET` | `/api/admin/roles` | List seeded authorization roles |
| `GET/POST/DELETE` | `/api/admin/users/{userId}/roles` | View, assign, and revoke user roles |
| `GET/POST` | `/api/admin/model-catalog` | Manage Admin-owned model catalog entries |
| `GET/POST/PUT/DELETE` | `/api/documents` | Manage source documents owned by the current user |
| `POST` | `/api/documents/{id}/vectorize` | Rebuild knowledge chunks for an owned source document |
| `POST` | `/api/knowledge/search` | Search tutor-scoped knowledge chunks |
| `GET/POST/PUT` | `/api/tutors` | Manage owned tutors |
| `POST/PUT` | `/api/tutors/{tutorId}/...` | Manage tutor prompts, tools, knowledge sources, model connection, deployment profile, and snapshots |

See Swagger UI (`/swagger`) for full request/response schemas.

### VS Code Extensions

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

### Branch Convention

```
[type]/[name]/[ticket_id]
```

Examples: `feature/login/BC-11`, `fix/chunk-search/BC-42`, `chore/clean-arch/BC-15`

## Project Structure

```
tutor-studio/                  # React + Vite frontend
src/
├── Bitcraft.Api/              # HTTP entry point, controllers, composition root
├── Bitcraft.Application/      # Use cases organized by business capability
├── Bitcraft.Domain/           # Core business concepts and rules
└── Bitcraft.Infrastructure/   # EF Core, pgvector, persistence and integrations
tests/
└── Bitcraft.UnitTests/
```

### Current capability grouping

- **Documents** — source-of-truth source artifacts owned by users
- **Knowledge** — vectorized retrieval derived from documents and scoped through tutors
- **Tutoring** — tutor configuration, prompts, tools, model connections, knowledge links, deployment profiles, and snapshots
- **Admin** — model catalog management and Admin/Editor/Viewer role management
- **Infrastructure/Persistence** — database mappings, repository implementations, migrations, pgvector, Identity, and Ollama adapters
- **Api** — thin controllers using request/response contracts, Swagger metadata, JWT auth, and role policies

**Dependency direction:** Api → Application + Infrastructure, Infrastructure → Application + Domain, Application → Domain. Core (Domain + Application) never references Infrastructure or Api.
