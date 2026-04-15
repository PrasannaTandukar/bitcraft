# Bitcraft

Agentic AI platform with document embeddings, vector search, and RAG capabilities.

## Tech Stack

- **Backend:** ASP.NET Core (.NET 10), Entity Framework Core, PostgreSQL + pgvector
- **Frontend:** React + Vite
- **AI:** Ollama (local inference)
- **Architecture:** Clean Architecture (screaming, feature-based)

## Getting Started with Development

### Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0+ | [Download](https://dotnet.microsoft.com/download) |
| Docker Desktop | Latest | Required for local infrastructure |
| Node.js | 20+ | Frontend only |

### Quick Start (Full Stack)

```bash
docker compose up
```

This starts: PostgreSQL + pgvector, Ollama + embedding model, backend API (port 8000), frontend (port 5173), and runs EF Core migrations automatically.

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

The API will be available at `http://localhost:5260` with Swagger UI at `/swagger`.

### EF Core Migration Cheatsheet

```bash
# Add a new migration
dotnet ef migrations add <MigrationName> --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api

# Apply pending migrations
dotnet ef database update --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api

# Remove last migration (if not applied)
dotnet ef migrations remove --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api
```

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
src/
├── Bitcraft.Api/              # HTTP entry point, controllers, composition root
├── Bitcraft.Application/      # Use cases organized by business capability
├── Bitcraft.Domain/           # Core business concepts and rules
└── Bitcraft.Infrastructure/   # EF Core, pgvector, persistence and integrations
tests/
└── Bitcraft.UnitTests/
```

### Current capability grouping

- **Knowledge** — document storage, chunk creation, semantic search
- **Infrastructure/Persistence** — database mappings, repository implementations, migrations
- **Api** — thin endpoints calling application handlers

**Dependency direction:** Api → Application + Infrastructure, Infrastructure → Application + Domain, Application → Domain. Core (Domain + Application) never references Infrastructure or Api.