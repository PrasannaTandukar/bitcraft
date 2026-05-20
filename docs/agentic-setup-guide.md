# Bitcraft Agentic Stack — Portable Setup Guide

This guide explains how to recreate a stack like Bitcraft's in another project. It is opinionated, GitHub Copilot Pro friendly, and compatible with third-party coding agents through the [agents.md](https://agents.md/) standard.

Use it as a template, not a prescription. Adjust agent count, prompt names, and governance docs to your project's reality.

## What You Are Building

A repository-versioned agentic AI layer with:

- A short, departmental agent roster.
- Lean prompt entrypoints for the common workflows.
- Reusable skills for procedures.
- Scoped instruction files for path-based rules.
- Per-agent prioritized inbox backlogs.
- Governance docs for project brief, metrics, model routing, audit templates, and framework decisions.
- A minimal read-only MCP server.
- Human-owned Git operations and credential surfaces.

## Directory Scaffold

```text
<repo>/
├── AGENTS.md                                # agents.md standard entry for third-party agents
├── .vscode/
│   └── mcp.json                             # MCP server wiring (selectively un-ignored)
├── .github/
│   ├── copilot-instructions.md              # always-on rules for Copilot
│   ├── instructions/
│   │   └── <topic>.instructions.md          # `applyTo` path-scoped rules
│   ├── agents/
│   │   ├── ROSTER.md                        # department directory
│   │   └── <slug>.agent.md                  # one file per custom agent
│   ├── prompts/
│   │   └── <verb>.prompt.md                 # workflow entrypoints
│   ├── skills/
│   │   └── <name>/SKILL.md                  # reusable procedures
│   ├── inboxes/
│   │   ├── README.md                        # inbox schema and rules
│   │   └── <agent-slug>/inbox.md            # prioritized backlog per agent
│   ├── agentic-ai/
│   │   ├── PROJECT_BRIEF.md
│   │   ├── TOOLING_MATRIX.md
│   │   ├── DEVELOPER_GUIDE.md
│   │   ├── METRICS.md
│   │   ├── MODEL_ROUTING.md
│   │   ├── AUDIT_TEMPLATES.md
│   │   └── FRAMEWORK_REVIEW.md
│   └── mcp/
│       └── <project>-mcp-spec.md            # full MCP design surface
├── mcp-server/                              # minimal read-only MCP server
│   ├── server.py
│   ├── requirements.txt
│   └── README.md
└── <subproject>/AGENTS.md                   # optional nested files for sub-areas
```

The `.github/` folder follows the [GitHub Copilot customization cheat sheet](https://docs.github.com/en/copilot/reference/customization-cheat-sheet). The repo-root `AGENTS.md` and nested files follow agents.md.

## Step-By-Step

### 1. Lock the always-on rules

Create `.github/copilot-instructions.md`. Keep it short. State:

- Project identity and stack in two sentences.
- Operating model (humans vs agents).
- Hard boundaries (no Git writes, no `.gitignore` edits, no secrets, no domain entity leaks, etc.).
- Architecture rules (dependency direction, immutability conventions).
- Coding standards (language version, naming, error model).
- Validation expectations.

Avoid duplicating the rules in every agent.

### 2. Add the agents.md entry

Create `AGENTS.md` at the repo root. It is the cross-tool agent entry for Codex, Aider, Junie, Cursor, Gemini CLI, OpenCode, Devin, and the Copilot cloud agent. Sections to include: project layout, build/test commands, architecture boundaries, security considerations, and a pointer to `.github/copilot-instructions.md` for Copilot-specific behavior.

For monorepos, add nested `AGENTS.md` inside each subproject.

### 3. Define a departmental roster

Pick fewer agents than you think you need. Use departments to keep concerns clean. Bitcraft uses:

- Admin: Secretary, Typewriter.
- Planning: Researcher, Planner.
- Development: Lead Dev (orchestrator), specialists (backend, frontend, infra).
- Integrity: Dev Ops, Sec Ops, AI Ops.

Each agent file (`.github/agents/<slug>.agent.md`) has frontmatter with `name`, `description`, `tools`, `target`, `agents`, and optional `handoffs:` UI buttons. The body has Required Context, Responsibilities, Constraints, Output. Keep it under one screen.

### 4. Write lean prompt files

Use prompts as verbs that map onto workflows, not as documentation. Suggested initial set:

- `overview.prompt.md` — work status.
- `research.prompt.md` — evidence gathering.
- `plan.prompt.md` — technical plan.
- `develop.prompt.md` — implementation entrypoint.
- `review.prompt.md` — impact-aware review.
- `audit-security.prompt.md`, `audit-integrity.prompt.md`, `audit-agentic.prompt.md` — recurring audits.
- `write.prompt.md` — docs and reports.
- `metrics.prompt.md` — metrics review and reflection.
- `compact.prompt.md` — session compaction into inbox items.

Each prompt has `description`, `agent`, `argument-hint` frontmatter and a short body with Procedure, Constraints, Output.

### 5. Build a skills library

Skills (`.github/skills/<name>/SKILL.md`) hold reusable procedures that prompts and agents call into. Examples: feature implementation, change risk classification, model routing, review checklist, agentic stack maintenance, context management. Each skill has `name`, `description`, `argument-hint` frontmatter.

Naming: lowercase, hyphenated, optionally prefixed with the project name (e.g., `bitcraft-review`).

### 6. Add scoped instructions

`.github/instructions/<topic>.instructions.md` files apply rules to file globs via the `applyTo` frontmatter. Examples: backend architecture rules, API contract rules, test rules, frontend rules, docs rules, agentic customization rules. Keep each focused on one path scope.

### 7. Set up the inbox backlog system

Inboxes are repository-versioned prioritized backlogs, one per agent. They serve four roles:

- Batching future work so an agent picks it up next time it is activated.
- Cross-agent delegation without seizing the current turn.
- Aggregation across roles by the Secretary (or equivalent).
- Continuity across session compaction and model restarts.

Schema (one item):

```markdown
- Date: YYYY-MM-DD
- Status: active | blocked | parked
- Source: <agent or Human>
- Target: <agent-slug>
- Priority: high | medium | low
- Task: One concrete actionable sentence.
- Context: Minimum safe context and links.
- Acceptance/Cleanup: Completion check and whether to remove.
```

Document the schema in `inboxes/README.md`. Refuse anything that is not a backlog item.

### 8. Write the governance docs

`.github/agentic-ai/`:

- `PROJECT_BRIEF.md`: purpose, surfaces, departments, constraints, model direction.
- `TOOLING_MATRIX.md`: which IDE/agent surfaces each primitive lives in.
- `DEVELOPER_GUIDE.md`: workflow, delegation rules, review routing, branching direction, metrics capture, drift control.
- `METRICS.md`: risk metrics (CSS, DIS, CIL, CSR, ILR, DORA) and execution metrics. Keep thresholds human-owned.
- `MODEL_ROUTING.md`: family-based routing for GPT, Claude, Gemini, local Ollama. No hardcoded model identifiers.
- `AUDIT_TEMPLATES.md`: Security, Integrity, Agentic audit sections with a shared finding schema.
- `FRAMEWORK_REVIEW.md`: dated cohesion-pass log with trigger, research checks, decisions, deviations, follow-ups.

### 9. Add a minimal MCP server

Start with a read-only Python MCP server that exposes only what your agents repeatedly need. Bitcraft's minimum:

- `list_inboxes`, `read_inbox(agent_slug)`.
- `list_governance_docs`, `read_governance(name)`.
- `list_agents`, `read_roster`.

Use the official Python `mcp` SDK (`FastMCP`). Constrain file access to allowed directories with a `_safe_read` helper that resolves and validates paths. Never accept free-form file paths from the agent.

Wire the server via `.vscode/mcp.json` with `${workspaceFolder}` and `stdio` transport. Selectively un-ignore `.vscode/mcp.json` in `.gitignore` so the wiring is shared.

Document scope, install, and security in `mcp-server/README.md`. Keep the full MCP design surface (future tools) in `.github/mcp/<project>-mcp-spec.md`.

### 10. Configure `.gitignore` for agentic teams

Treat agentic AI files as first-class versioned artifacts:

- Keep `.github/**` versioned including instructions, prompts, agents, skills, inboxes, governance docs, and MCP spec.
- Keep `AGENTS.md` versioned (root and nested).
- Selectively un-ignore `.vscode/mcp.json`, `settings.json`, `extensions.json`, `tasks.json`, `launch.json` so MCP and shared workspace config are sharable.
- Ignore agent scratch artifacts that should never be committed: `*.session.md`, `*.draft.md`, `*.transcript.md`.
- Do not let agents modify `.gitignore` without explicit human approval and clear security rationale.

Agent-private memory (runtime `/memories/`, `/memories/session/`, `/memories/repo/`) lives in the user profile, not the repository, so no entry is needed.

### 11. Define model routing

Pick by family, not by exact identifier (model names rotate):

- Cloud frontier (GPT, Claude) for primary coding, planning, and review.
- Gemini for long-context research and writing-heavy synthesis.
- Local Ollama (DeepSeek Coder, Qwen Coder) for bounded refactors, summarization, future Junior Dev experiments, and privacy-sensitive workflows.

Document the routing table in `MODEL_ROUTING.md` and reflect on it after each major audit. Never paste secrets or unredacted customer data into cloud inference.

### 12. Run recurring audits

Three audit lanes:

- Security audit (Sec Ops).
- Integrity audit (Dev Ops).
- Agentic audit (AI Ops).

Use `AUDIT_TEMPLATES.md`. Each finding gets a Severity, CIL, CSS, DIS, Owner, Evidence, Risk, Recommendation, and Follow-up. Audit findings always convert into inbox items. Never let audits become unactioned report sprawl.

### 13. Validate before merging stack changes

For any change in `.github/` or `AGENTS.md`:

- Frontmatter parses cleanly.
- Naming conventions hold (kebab-case for skills/prompts/inboxes, Title Case for agent `name`, lowercase slugs for agent files).
- Internal links resolve.
- No stale references after a rename.
- No secrets, credentials, dataset paths, or model weights anywhere.
- If `.vscode/mcp.json` changed: confirm all third-party server tools are in the approved tool list, and that per-request approval is configured for any server with prompt-injection risk.

Pair this with the project's normal lint/test pipelines.

## Naming Conventions Cheat Sheet

| Artifact | Convention | Example |
| --- | --- | --- |
| Repo root agent entry | `AGENTS.md` | `AGENTS.md` |
| Subproject agent entry | `<subproject>/AGENTS.md` | `tutor-studio/AGENTS.md` |
| Copilot always-on rules | `.github/copilot-instructions.md` | — |
| Scoped instruction | `.github/instructions/<topic>.instructions.md` | `backend-clean-architecture.instructions.md` |
| Agent file | `.github/agents/<slug>.agent.md` | `lead-dev.agent.md` |
| Roster | `.github/agents/ROSTER.md` | — |
| Prompt | `.github/prompts/<verb-or-noun>.prompt.md` | `develop.prompt.md` |
| Skill | `.github/skills/<name>/SKILL.md` | `bitcraft-review/SKILL.md` |
| Inbox | `.github/inboxes/<agent-slug>/inbox.md` | `ai-ops/inbox.md` |
| Governance doc | `.github/agentic-ai/<NAME>.md` | `MODEL_ROUTING.md` |
| MCP spec | `.github/mcp/<project>-mcp-spec.md` | `bitcraft-mcp-spec.md` |
| MCP server | `mcp-server/server.py` | — |
| Audit report | `.github/agentic-ai/AUDIT_<YYYY-MM-DD>_<LANE>.md` | `AUDIT_2026-05-18_AGENTIC.md` |

Agent slugs are lowercase hyphenated. Agent display names in frontmatter are Title Case. Skill names are lowercase hyphenated, optionally prefixed with the project name.

## Anti-Overengineering Guardrails

- Do not add an agent when a skill or a prompt will do.
- Do not add a prompt when an existing workflow already routes the work.
- Do not duplicate long policy text across prompts, agents, and skills. Link instead.
- Do not turn inboxes into chat logs or reports.
- Do not hard-code model identifiers in frontmatter.
- Do not make MCP write tools default. Read-only is the baseline.
- Do not let agents perform Git writes.
- Do not let audits exist without inbox follow-up items.

## When To Cohesion-Pass

Trigger a cohesion pass when any of:

- The agent roster contains overlapping responsibilities.
- A workflow keeps failing in the same way across sessions.
- Naming has drifted (mixed slug styles, dead references).
- A new Copilot or MCP primitive becomes generally available.
- A new model family materially shifts what cloud vs local can do.

Log the pass in `FRAMEWORK_REVIEW.md` with trigger, research checks, decisions, deviations, follow-ups.
