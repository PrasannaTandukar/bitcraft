---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# Bitcraft Agent Catalog

Custom agents live in this folder as `.agent.md` files. Use them when the task benefits from a stable role, tool policy, inbox ownership, and optional guided handoff.

## Admin

- `secretary`: overview, timelines, inbox triage, roadmaps, Jira/Bitbucket status, and work coordination.
- `typewriter`: reports, guides, README/docs, audit writeups, and presentable developer documentation.

## Planning

- `researcher`: repository and external evidence gathering, academic/technical research, and source verification.
- `planner`: technical planning, risk classification, impacted files, acceptance criteria, and validation strategy.

## Development

- `lead-dev`: full-stack development orchestrator, expert delegation, review routing, and fallback implementation.
- `net-dev`: .NET backend, Clean Architecture, EF Core, API/Application/Domain/Infrastructure, and backend tests.
- `web-dev`: React/Vite Tutor Studio frontend and frontend validation.

## Integrity

- `dev-ops`: CI/CD, Docker, deployment, migrations, branch conventions, release workflow, and integrity audit.
- `sec-ops`: security, privacy, authorization, prompt-injection, data governance, and security audit.
- `ai-ops`: prompts, agents, instructions, skills, MCP, model routing, metrics, and agentic audit.

## Coordination Rules

- Lead Dev owns development delegation. Do not bypass Lead Dev for multi-agent development work unless the user directly asks for a specialist.
- Secretary owns overview and inbox hygiene, not product authority.
- AI Ops owns the agentic stack and should absorb prompt-engineering, MCP, and model-routing work.
- Use `.github/inboxes/<agent-slug>/inbox.md` as the prioritized backlog per agent. Remove completed items.
- Use `send: false` on agent `handoffs:` blocks when a UI handoff should preserve human review before sending.
- Do not duplicate long procedures that belong in `.github/skills` or `.github/governance`.
