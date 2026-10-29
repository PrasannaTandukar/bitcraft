---
created_at: 2026-05-21
edited_at: 2026-05-21
creator: secretary
editor: secretary
ai_gen: true
---
# Deployment Changes Report — 2026-05-21

> **Audience**: Human reviewer + DevOps. Emphasis on what changed, why it matters, and what you need to do before first deployment.  
> **Scope**: credential management, initial admin provisioning, the build-level auth bypass toggle, and compose health-gate changes from the local-setup revision.

---

## 1. Credential Management

### What changed

The codebase now has **zero hardcoded credentials in source code**. All deployment-sensitive values are read from environment variables at runtime.

Previously the seeder had hardcoded admin credentials. These were removed and replaced with env-var-first lookup (D9, [AGENTS.md §Security](../../AGENTS.md)).

### How configuration flows

```
.env (local, git-ignored)
  ↓ loaded by Docker Compose
docker-compose.yml environment: block
  ↓ injected into container
ASP.NET Core app reads via Environment.GetEnvironmentVariable()
  or via appsettings.json default values (compose-internal only)
```

**Key distinction for development:**
- `appsettings.json` — tracked file; contains compose-internal defaults (`Host=db`, the default passwords). Fine in dev because the defaults are per-deployment and the database is not exposed publicly.
- `.env` file — **git-ignored**; overrides those defaults for the local machine. Copy from `.env.example` and edit.
- Production — injects via CI/CD secret store or container orchestrator secrets; never from a tracked file.

### The `.env.example` file

[`.env.example`](../../.env.example) (tracked, safe to commit) shows every available variable with its default and a comment. Copy to `.env` to get a working local setup:

```bash
cp .env.example .env
# then edit .env to set BITCRAFT_ADMIN_EMAIL and BITCRAFT_ADMIN_PASSWORD
```

**Variables added in this batch:**

| Variable | Default | Purpose |
|----------|---------|---------|
| `POSTGRES_DB` | `bitcraft` | Database name |
| `POSTGRES_USER` | `bitcraft_user` | Database user |
| `POSTGRES_PASSWORD` | `bitcraft_password` | Database password |
| `OLLAMA_CHAT_MODEL` | `llama3.2:3b` | Chat model to pull and use |
| `OLLAMA_EMBED_MODEL` | `qwen3-embedding:0.6b` | Embedding model |
| `OLLAMA_EMBED_DIMENSIONS` | `1024` | Embedding vector dimensions |
| `VITE_USE_POLLING` | `false` | Enable Vite file-watcher polling (WSL2 / some macOS) |
| `BITCRAFT_ADMIN_EMAIL` | *(not set)* | Bootstrap admin email — see §2 below |
| `BITCRAFT_ADMIN_PASSWORD` | *(not set)* | Bootstrap admin password — see §2 below |

> **Learner note — Why env vars?** The Twelve-Factor App method ([Factor III](https://12factor.net/config)) says configuration that differs between environments (dev, staging, prod) must come from the environment, not from tracked files. This prevents the classic "works on my machine" problem and ensures production secrets never end up in git history.

---

## 2. Initial Admin User Seeding

### What changed

Admin provisioning is now **fully opt-in via environment variables**. The seeder no longer has a fallback or default.

### How it works

[`src/Bitcraft.Infrastructure/Identity/IdentityRoleSeeder.cs`](../../src/Bitcraft.Infrastructure/Identity/IdentityRoleSeeder.cs) runs at every startup. It does two things:

1. **Role seeding** (always runs): ensures `Admin`, `Editor`, `Auditor` roles exist in the database. This is idempotent — safe to run every restart.
2. **Admin bootstrap** (conditional): checks for both `BITCRAFT_ADMIN_EMAIL` and `BITCRAFT_ADMIN_PASSWORD` env vars. If **either** is absent or empty, it logs a warning and **skips** — no partial creation, no default account, no weak password.

```
BITCRAFT_ADMIN_EMAIL=admin@example.com
BITCRAFT_ADMIN_PASSWORD=<strong-password>

→ seeder creates the account if it doesn't exist
→ assigns Admin role
→ subsequent restarts: idempotent (finds existing account, skips create)

BITCRAFT_ADMIN_EMAIL= (empty)
BITCRAFT_ADMIN_PASSWORD= (empty)

→ seeder logs: "Bootstrap admin seeding skipped: env vars not set"
→ no account created; no error thrown
```

The password is passed through ASP.NET Core Identity's built-in password policy validation (minimum length, complexity). If the policy rejects it, startup throws `InvalidOperationException` — there is no silent weak-password path.

### What you must do for first deployment

1. Set `BITCRAFT_ADMIN_EMAIL` and `BITCRAFT_ADMIN_PASSWORD` in your deployment environment (`.env` locally, CI/CD secrets in production).
2. Start the API. Check startup logs for the confirmation message or the skip warning.
3. Log in with those credentials and get a JWT. The JWT will carry the `Admin` role claim.
4. Use that Admin token to invite or promote other users.

> **Learner note — Why not auto-assign the first registered user?** Auto-promotion creates a race condition in multi-instance deployments and can create privilege escalation opportunities in automated tests. Explicit env-var provisioning makes the admin account creation auditable and deliberate.

### Changing the admin password

ASP.NET Core Identity stores a hashed password (PBKDF2-SHA256). Changing it requires an Identity-level password reset flow (not implemented yet) or a direct Identity user update through an Admin-authenticated API call. **Never update the hash column in the database directly.**

---

## 3. Build-Level Auth Bypass Toggle (`BITCRAFT_RUNTIME_LOCAL_ONLY`)

### What changed

A repo-root `Directory.Build.props` file defines the `BITCRAFT_RUNTIME_LOCAL_ONLY` MSBuild conditional compilation symbol. This is the only mechanism that enables anonymous access to the runtime (chat) endpoint.

### How it works

```xml
<!-- Directory.Build.props -->
<PropertyGroup Condition="'$(Configuration)' == 'Local'">
  <DefineConstants>$(DefineConstants);BITCRAFT_RUNTIME_LOCAL_ONLY</DefineConstants>
</PropertyGroup>
```

In C# source, any code path that should only exist locally is wrapped with `#if BITCRAFT_RUNTIME_LOCAL_ONLY ... #endif`. When this symbol is **not** defined, the compiler literally excludes that code from the assembly — it does not exist in the binary at all.

**MSBuild configurations and the flag:**

| MSBuild Configuration | Symbol defined? | Who uses it? |
|-----------------------|-----------------|-------------|
| `Debug` | ❌ No | Normal development (`dotnet run`) |
| `Release` | ❌ No | Production builds |
| `Local` | ✅ Yes | Local PoC / benchmarking workflow only |
| CI | ❌ No | CI never sets `Configuration=Local` |

To build locally with the flag:

```bash
dotnet build -c Local
dotnet run --project src/Bitcraft.Api -c Local
```

> **Learner note — Why a compile-time flag instead of an appsettings switch?** A runtime config switch (`"AllowAnonymous": true` in appsettings) can be accidentally set in production through a misconfigured deployment. A compile-time flag is physically impossible to misconfigure — the anonymous code path is simply not in the shipped binary. This is [twelve-factor Factor V (Build, Release, Run)](https://12factor.net/build-release-run) applied to security.

### What you must verify before any non-local deployment

Run a reflection-based check to confirm the anonymous controller variant is absent:

```bash
dotnet build -c Release
# then in a test or CI step:
# load the assembly and confirm no controller action is decorated with [AllowAnonymous]
# except HealthController and AuthController (which are explicitly intended to be anonymous)
```

The planned reflection-based assembly inspection test (from `plan.md`) would automate this. Until that test lands, manually verify with a curl or HTTP file test against the runtime endpoint without a token — it must return `401`.

---

## 4. Docker Compose Health Gates

### What changed

Backend healthcheck added. `tutor-studio` now waits for the backend to be healthy before starting.

### Backend Dockerfile HEALTHCHECK

```dockerfile
# src/Bitcraft.Api/Dockerfile
RUN apt-get install -y curl   # added — required for the healthcheck CMD

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=10 \
  CMD curl -fsS http://127.0.0.1:8000/api/health || exit 1
```

The `/api/health` endpoint ([`src/Bitcraft.Api/Controllers/HealthController.cs`](../../src/Bitcraft.Api/Controllers/HealthController.cs)) returns:

```json
{ "status": "ok", "version": "1.0.0.0" }
```

`[AllowAnonymous]` — no JWT needed. This is intentional: health-check callers (Docker, load balancers, orchestrators) do not have tokens.

> **Security note:** The `version` field is an assembly version fingerprint. It is acceptable in a dev/PoC context. Before public production deployment, consider gating it behind `IHostEnvironment.IsDevelopment()` — see net-dev inbox L-1 item.

### Compose `depends_on` changes

Three compose files now gate `tutor-studio` on `service_healthy` instead of just `service_started`:

```yaml
# docker-compose.yml, docker-compose.dev.yml, docker-compose.prod.yml
tutor-studio:
  depends_on:
    backend:
      condition: service_healthy   # was: service_started (implicit)
```

**Effect**: the frontend container will not start serving requests until the backend passes its healthcheck. This prevents the React app from crashing on startup because the API isn't ready yet.

The AI compose overrides (`docker-compose.ai.yml`, `docker-compose.ai.gpu.yml`, `docker-compose.ai.jetson.yml`) add the Ollama service healthcheck + pull services — the backend waits for both Ollama to be healthy and the model pulls to complete before starting.

---

## 5. What Needs to Happen Before a Production Deployment

In priority order:

| # | Action | Where |
|---|--------|-------|
| 1 | Apply all 8 EF migrations in D25 order | Dev Ops inbox — each `dotnet ef database update` command is documented |
| 2 | Set `BITCRAFT_ADMIN_EMAIL` + `BITCRAFT_ADMIN_PASSWORD` in the deployment secret store | Production environment only; never in `.env.example` |
| 3 | Build with `dotnet build -c Release` (not `-c Local`) | CI pipeline |
| 4 | Confirm `/api/runtime-endpoint` (chat) returns `401` without a token | Smoke test |
| 5 | Confirm `/api/health` returns `200 {"status":"ok"}` | Smoke test (Docker health gate) |
| 6 | Gate the `version` field in `/api/health` behind `IsDevelopment()` | Net-dev L-1 item |
| 7 | Add rate limiting to `/api/health` | Net-dev L-1 item |
| 8 | Set the ANN strategy env var for the embedding model | Dev Ops item — HNSW was removed; sequential scan active |

---

## 6. Current Auth Bypass Toggle Status

| Context | Anonymous chat? | Mechanism |
|---------|----------------|-----------|
| `dotnet run` (Debug) | ❌ No | JWT required |
| `dotnet run -c Local` | ✅ Yes | `BITCRAFT_RUNTIME_LOCAL_ONLY` defined |
| Docker Compose (any) | ❌ No | Compose runs the published Release image |
| CI build | ❌ No | CI uses Debug/Release, never Local |
| Production | ❌ No | Release build, symbol not defined |

The toggle is **one-way** for production safety: no runtime switch, no config file flag, no environment variable can enable anonymous access in a shipped Release build. The only path to anonymous chat access is building with `dotnet build -c Local` on a developer machine.
