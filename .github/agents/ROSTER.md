---
created_at: 2026-05-19
edited_at: 2026-09-17
creator: ai-ops
editor: ai-ops
ai_gen: true
---

# Bitcraft Agent Catalog

Custom agents live in this folder as `.agent.md` files. Use them when the task benefits from a stable role, tool policy, inbox ownership, and optional guided handoff.

Five agents cover the plan → develop → review workflow. Capabilities without a dedicated agent (research, documentation, backend/frontend specialization, releases) live as skills under `.github/skills/` and run inside the owning agent.

## Coordination

- `secretary` (Secretary): work overview, inbox triage, roadmaps, Jira/Bitbucket status, coordination, **and** documentation/reports/audit writeups (absorbed Typewriter). The all-rounder that covers what the dev stack does not.

## Planning

- `planner` (Planner): technical planning, risk classification (CSS/DIS/CIL), impacted files, acceptance criteria, and validation strategy. Research is a skill invoked inside Planner or Secretary, not a separate agent.

## Development

- `lead-dev` (Lead Dev): full-stack implementation and validation. Owns backend (.NET, EF Core, Clean Architecture) and frontend (React/Vite Tutor Studio) directly — NET Dev and Web Dev were consolidated into this role.

## Integrity

- `sec-ops` (Sec Ops): security, privacy, authorization, prompt-injection, data governance, **plus** CI/CD, Docker, deployment, migrations, release workflow, and branch conventions (absorbed Dev Ops). Single integrity owner.
- `ai-ops` (AI Ops): prompts, agents, instructions, skills, MCP, model routing, metrics, and agentic audit.

## Naming Convention

- Each agent file's YAML `name:` value is the **Title Case display name** (e.g. `Lead Dev`, `Sec Ops`, `AI Ops`).
- The **slug** is the kebab-case folder/file/inbox identifier (e.g. `lead-dev`, `sec-ops`, `ai-ops`) and is used in paths, MCP tool arguments, and inbox routing.
- Cross-references in inboxes, plans, prompts, and skills should use the slug for paths and the display name for prose.

## Coordination Rules

- Lead Dev owns development execution. Secretary handles upstream triage; Lead Dev handles downstream implementation.
- Secretary owns overview, inbox hygiene, and documentation output, not product authority.
- AI Ops owns the agentic stack and absorbs prompt-engineering, MCP, and model-routing work.
- Use `.github/inboxes/<agent-slug>/inbox.md` as an optional local backlog per agent. Git ignores this folder. Remove completed items and move durable outcomes to tracked static documents.
- Use `send: false` on agent `handoffs:` blocks when a UI handoff should preserve human review before sending.
- Do not duplicate long procedures that belong in `.github/skills` or `.github/governance`.

## Retired Agents (2026-09-17)

Researcher, NET Dev, Web Dev, Dev Ops, and Typewriter were consolidated into the five agents above. Their capabilities survive as skills (`bitcraft-research`, `bitcraft-backend-feature`, `bitcraft-frontend-feature`, `bitcraft-devops-release`, `bitcraft-doc-maintenance`) and their open inbox items were merged into the surviving inboxes.
