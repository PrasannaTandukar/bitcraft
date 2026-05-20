---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# Bitcraft Agentic AI Project Brief

## Purpose

Bitcraft is an Agentic AI Tutor management platform. The repository combines a .NET 10 Clean Architecture backend, PostgreSQL/pgvector infrastructure, Identity/JWT security, a Python sidecar, and a React/Vite Tutor Studio frontend.

The agentic AI layer exists to improve development coordination, quality, repeatability, and reflection. It does not replace human project ownership.

## Human And Agent Cooperation

- Humans own product direction, priority, credentials, branch creation, commits, pushes, merges, releases, and final sign-off.
- Agents provide evidence gathering, planning, implementation, testing, review, documentation, audit support, and workflow reflection.
- Agents may create inbox items for future work. Inbox items are backlog, not authority.
- Jira and Bitbucket may become work-management inputs, but credentials and active integration config stay outside the repository.

## Repository Surfaces

- Backend source: `src/Bitcraft.Domain`, `src/Bitcraft.Application`, `src/Bitcraft.Infrastructure`, `src/Bitcraft.Api`.
- Backend tests: `tests/Bitcraft.UnitTests`, `tests/Bitcraft.ApiIntegrationTests`, `tests/Bitcraft.InfrastructureIntegrationTests`.
- Frontend: `tutor-studio/` with a local `AGENTS.md` for coding agents.
- Python sidecar: `sidecar/` and `tests/Bitcraft.SidecarIntegration`.
- Agentic operational layer: `.github/` (instructions, prompts, agents, skills, inboxes, governance docs, MCP spec).
- Cross-tool agent entry: `AGENTS.md` at the repository root, per the [agents.md](https://agents.md/) standard.
- Local MCP server: `mcp-server/` (read-only, exposes inboxes, roster, and governance docs).
- Human-facing docs: `docs/`.

## Agent Departments

- Admin: Secretary and Typewriter.
- Planning: Researcher and Planner.
- Development: Lead Dev, NET Dev, and Web Dev.
- Integrity: Dev Ops, Sec Ops, and AI Ops.

Lead Dev is the development orchestrator. AI Ops owns the agentic AI stack. Secretary owns work overview and inbox hygiene. Typewriter owns presentable docs and reports.

## Current Priorities

1. Keep the agentic stack lean, cohesive, and measurable.
2. Establish inbox-backed cross-agent coordination.
3. Establish metrics for change risk, delivery effectiveness, agent execution, model routing, and incident leakage.
4. Create a Bitbucket/Jira-compatible branching convention after this stack migration, with Git write operations remaining human-owned.
5. Use recurring Security, Integrity, and Agentic audits to create actionable iterative work rather than one-off reports.

## Constraints

- No secrets or credentials in `.github`, docs, prompts, examples, or MCP templates.
- No write-capable MCP defaults.
- No Git write operations by agents.
- No production data access by default.
- No domain entities exposed through API contracts.
- No external package references from `src/Bitcraft.Application`.

## Model Direction

Use model families deliberately:

- Copilot GPT and Claude families for main coding, review, planning, and architecture work.
- Gemini family for deep research and writing-heavy synthesis when available.
- Local Ollama models, including DeepSeek/Qwen families on the RTX 3090 setup, for bounded low-risk summarization, drafting, and future junior-dev style tasks.

Do not hard-code exact model identifiers into agent frontmatter until availability is verified in the active Copilot client.
