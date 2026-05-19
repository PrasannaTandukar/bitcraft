---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: human
editor: lead-dev
ai_gen: false
---
# AGENTS.md

Bitcraft is an Agentic AI Tutor management platform. This file is the entry point for any third-party coding agent (Codex, Aider, Junie, Cursor, Gemini CLI, OpenCode, Devin, Copilot cloud agent, etc.) per the [agents.md](https://agents.md/) standard.

GitHub Copilot agents in VS Code should also read `.github/copilot-instructions.md`, which is the always-on rule set for this repository.

## Project Layout

- `src/Bitcraft.Domain`, `src/Bitcraft.Application`, `src/Bitcraft.Infrastructure`, `src/Bitcraft.Api`: .NET 10 Clean Architecture backend.
- `tests/Bitcraft.UnitTests`, `tests/Bitcraft.ApiIntegrationTests`, `tests/Bitcraft.InfrastructureIntegrationTests`, `tests/Bitcraft.SidecarIntegration`: backend and sidecar tests.
- `tutor-studio/`: React/Vite frontend. See `tutor-studio/AGENTS.md` for frontend-specific guidance.
- `sidecar/`: Python FastAPI sidecar with deepeval benchmarking.
- `.github/`: agentic operational layer (instructions, prompts, agents, skills, inboxes, governance docs, MCP spec).
- `docs/`: human-facing documentation.

## Build And Test

- Build: `dotnet build bitcraft.slnx`.
- Backend unit tests: `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj`.
- Run API locally: `dotnet run --project src/Bitcraft.Api/Bitcraft.Api.csproj`.
- EF migrations: `dotnet ef migrations add <name> --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api`.
- Frontend lint: from `tutor-studio/` run `npm run lint`.
- Frontend dev server: from `tutor-studio/` run `npm run dev`.

Run the narrowest relevant validation first. Broaden only when the change touches more surface.

## Architecture Boundaries

- Dependency flow: `Bitcraft.Api` -> `Bitcraft.Application` + `Bitcraft.Infrastructure`; `Bitcraft.Infrastructure` -> `Bitcraft.Application` + `Bitcraft.Domain`; `Bitcraft.Application` -> `Bitcraft.Domain`.
- `Bitcraft.Domain` and `Bitcraft.Application` must not reference `Bitcraft.Infrastructure` or `Bitcraft.Api`.
- `Bitcraft.Application` must not reference external packages.
- API contracts live in `src/Bitcraft.Api/Contracts`. Never expose Domain entities through API contracts.
- CQRS DTOs, requests, responses, and API contracts are immutable records unless a framework requires otherwise.

## Coding Standards

- C# 12+, file-scoped namespaces, primary constructors where appropriate, async I/O, cancellation tokens.
- Application uses a `Result<T>` pattern for expected failures; the API translates to HTTP.
- Interfaces prefixed with `I`, async methods suffixed with `Async`, DTOs suffixed with `Request`/`Response`, version snapshots suffixed with `Snapshot`, link tables suffixed with `Link`, tutor-scoped resources prefixed with `Tutor`.
- Hard delete is the default. Do not introduce soft-delete without an explicit feature need.

## Security Considerations

- No secrets, credentials, tokens, dataset paths, model weights, or production endpoints in this repository.
- Do not modify `.gitignore` without explicit human approval and a clear security rationale.
- Identity uses ASP.NET Core Identity with JWT. Authorization policies live in the API layer.
- Knowledge ingestion clears existing vectors for the document before re-embedding.

## Agent Operating Rules

- Humans own all Git write operations: branch creation, commit, push, merge, rebase, release. Agents do not perform Git writes.
- Humans own credentials, secrets, and final accountability.
- Agents propose changes, implement requested edits, run validation, and surface review needs.

## GitHub Copilot Agentic Stack

If you are GitHub Copilot in VS Code, the full agent department roster, prompt files, skills, instructions, inboxes, and governance docs live under `.github/`. Start with:

- `.github/copilot-instructions.md` for always-on rules.
- `.github/agents/ROSTER.md` for the agent department directory.
- `.github/governance/PROJECT_BRIEF.md`, `.github/governance/DEVELOPER_GUIDE.md`, `.github/governance/METRICS.md`, `.github/governance/MODEL_ROUTING.md` for governance.
- `.github/inboxes/<agent-slug>/inbox.md` for prioritized per-agent backlog.

Other coding agents only need this file plus repository code.

## PR Conventions

Branch naming and merge policy are owned by Dev Ops and humans, not by agents. Do not push or open PRs from an agent session.
