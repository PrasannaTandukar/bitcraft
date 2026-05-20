---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# Bitcraft Agentic Audit — 2026-05-18

- Auditor: AI Ops
- Scope: `.github/` agentic stack, repository-root `AGENTS.md`, `tutor-studio/AGENTS.md`, `mcp-server/`, `.vscode/mcp.json`, `.gitignore` agentic block, `docs/agentic-*.md`.
- Excludes: backend source, frontend source, sidecar code, EF migrations, deployment infra, Jira/Bitbucket account configuration.
- Reference: `.github/agentic-ai/AUDIT_TEMPLATES.md` (Agentic Audit section), [GitHub Copilot customization cheat sheet](https://docs.github.com/en/copilot/reference/customization-cheat-sheet), [agents.md](https://agents.md/).

## 1. Executive Summary

The stack is cohesive after Cohesion Pass 2 (see `FRAMEWORK_REVIEW.md`). All primitives identified in the GitHub Copilot customization cheat sheet are either implemented (`copilot-instructions.md`, `instructions/`, `agents/`, `prompts/`, `skills/`, MCP) or intentionally deferred (`hooks/`). The agents.md standard is honored via the new repository-root `AGENTS.md` and nested `tutor-studio/AGENTS.md`. The MCP layer transitioned from spec-only to a minimal read-only Python server. No Critical or High findings. Remaining work is human-owned (branch convention, metrics thresholds) or routine maintenance.

## 2. Stack Cohesion

| Surface | Status | Evidence |
| --- | --- | --- |
| `.github/copilot-instructions.md` | OK | References `ROSTER.md`, `inboxes/`, `METRICS.md`; no stale paths. |
| `.github/instructions/*.instructions.md` | OK | `applyTo` patterns scoped correctly to backend, contracts, tests, frontend, docs, customizations. |
| `.github/agents/*.agent.md` (10 files) | OK | Departmental roster intact. All inbox references point to `.github/inboxes/`. Title Case `name` field consistent. |
| `.github/agents/ROSTER.md` | OK | Departmental directory; no AGENTS.md collision. |
| `.github/prompts/*.prompt.md` (11 files) | OK | Each has `description` + `agent` frontmatter. Verb/noun naming intentionally pragmatic. |
| `.github/skills/*/SKILL.md` (18 skills) | OK | `name`, `description`, `argument-hint` present. Lowercase hyphenated. Context-management skill now integrates `/memories/` scopes. |
| `.github/inboxes/<slug>/inbox.md` (10 inboxes) | OK | Schema documented in `inboxes/README.md`. Prioritized-backlog purpose clarified. |
| `.github/agentic-ai/` | OK | `PROJECT_BRIEF`, `TOOLING_MATRIX`, `DEVELOPER_GUIDE`, `METRICS`, `MODEL_ROUTING` (new), `AUDIT_TEMPLATES`, `FRAMEWORK_REVIEW`. |
| `.github/mcp/bitcraft-mcp-spec.md` | OK | Now reflects implemented minimal baseline + deferred extensions. |
| `mcp-server/` | OK | Read-only Python MCP server. Path-traversal guard in `_safe_read`. Syntax verified. |
| `.vscode/mcp.json` | OK | Selectively un-ignored. Workspace-folder relative command. |
| `AGENTS.md` (repo root) | OK | Conforms to agents.md spec; routes Copilot to `.github/copilot-instructions.md`. |
| `tutor-studio/AGENTS.md` | OK | Nested frontend file per agents.md monorepo guidance. |
| `docs/agentic-ai.md`, `docs/agentic-architecture-base.md` | OK | Tables updated for MCP server, MODEL_ROUTING, compact prompt, AGENTS.md. |

## 3. Tool Policy, Model Routing, Prompt-Injection, Human-Owned Surfaces

- **Tool policy**: agent frontmatter uses generic `tools: ['read','search','edit','execute','todo','agent']`. No tool name hard-coded that does not exist in the Copilot tool taxonomy. Lead Dev retains `agent` access for delegation. Read-heavy roles do not hold `execute` unless their inbox/report editing demands it. No regressions.
- **Model routing**: now centralized in `.github/agentic-ai/MODEL_ROUTING.md` and `bitcraft-model-routing` skill. No frontmatter hard-codes model identifiers. Local Ollama paths are family-based (DeepSeek, Qwen) without specific model IDs.
- **Prompt-injection exposure**: MCP server returns only `.github/` content (inboxes, roster, governance docs). All content is repository-controlled and already trusted. No external/user content is surfaced. Path traversal is rejected. No external HTTP calls.
- **Human-owned surfaces**: `.gitignore` rules still forbid agent modifications without explicit approval. No agent has Git write tools enabled. `AGENTS.md` reiterates the boundary.

## 4. Metrics, Inbox Hygiene, Drift, Overengineering

- **Metrics capture**: `.github/agentic-ai/METRICS.md` defines CSS, DIS, CIL, CSR, ILR, DORA, plus agent-execution metrics. Thresholds remain deferred to human decision (Secretary inbox). No false precision introduced. **No issue.**
- **Inbox hygiene**: each agent has one active or parked item. Format follows `inboxes/README.md` schema. **No issue.**
- **Drift**: One stale grep cache match for `.github/agents/AGENTS.md` after the rename; file is confirmed deleted via `list_dir`. No persistent drift in tracked content. **No issue.**
- **Overengineering**:
  - The MCP server has exactly six tools — minimum useful surface (list/read for inboxes, governance, agents) without speculative database, contract, or test tools. **OK.**
  - 18 skills is large but each has a distinct trigger and Bitcraft prefix; no two skills overlap by name. **OK; revisit at next audit if duplication appears.**
  - `docs/agentic-architecture-base.md` partially duplicates `PROJECT_BRIEF.md` + `DEVELOPER_GUIDE.md`. **Low-severity finding F-1 below.**

## 5. Findings

### F-1: `docs/agentic-architecture-base.md` duplicates governance docs

- Severity: Low
- CIL: Low
- CSS: 1
- DIS: 1
- Owner: Typewriter
- Evidence: `docs/agentic-architecture-base.md` repeats sections that already live in `.github/agentic-ai/PROJECT_BRIEF.md` and `DEVELOPER_GUIDE.md`. Drift risk if one copy updates and the other does not.
- Risk: Documentation drift over time; reader confusion about source of truth.
- Recommendation: Trim `docs/agentic-architecture-base.md` to a brief "portable agentic template" reference that links into `.github/agentic-ai/`, or delete it and link `docs/agentic-ai.md` from any external setup material.
- Follow-up: Inbox item created on Typewriter.

### F-2: Repo memory is stale after the rename pass

- Severity: Low
- CIL: Low
- CSS: 1
- DIS: 2
- Owner: AI Ops (runtime)
- Evidence: `/memories/repo/bitcraft_agentic_ai_stack.md` and related entries were written before the rename of `handoffs` → `inboxes` and the addition of root `AGENTS.md`.
- Risk: Future sessions could load stale path facts and reintroduce wrong references.
- Recommendation: On next AI Ops activation, refresh the four `/memories/repo/*.md` entries to point at the new layout.
- Follow-up: Inbox item created on AI Ops.

### F-3: MCP server install/configure verification pending

- Severity: Informational
- CIL: Low
- CSS: 1
- DIS: 1
- Owner: AI Ops (with human approval to run)
- Evidence: `mcp-server/server.py` is syntactically valid and `pip install mcp` has not been executed in this environment.
- Risk: Configuration drift between `.vscode/mcp.json` and the Python environment used by VS Code.
- Recommendation: Human runs `python -m pip install -r mcp-server/requirements.txt` once, then VS Code reload + MCP server start; confirm `bitcraft-mcp` is listed in the MCP picker.
- Follow-up: Inbox item created on AI Ops.

### F-4: Branching convention still unowned policy

- Severity: Informational (carried over from prior audit)
- CIL: Medium
- CSS: 2
- DIS: 3
- Owner: Dev Ops + Human
- Evidence: `.github/agentic-ai/DEVELOPER_GUIDE.md` lists branching as planned; Dev Ops inbox carries the proposal item.
- Risk: Ad-hoc branch naming when human work picks up.
- Recommendation: No new action; the existing Dev Ops inbox item already tracks this.
- Follow-up: Already on Dev Ops inbox.

## 6. Human Decisions And Unresolved Risks

- Metrics thresholds and capture location (Secretary inbox).
- Bitbucket/Jira branch convention (Dev Ops inbox).
- Definition of agent-caused incident for CSR/ILR (Secretary inbox).
- When to extend the MCP server beyond the minimal six tools (deferred in `bitcraft-mcp-spec.md`).
- When to trial a local Junior Dev model with bounded tasks.

## 7. Result

- Critical findings: 0
- High findings: 0
- Medium findings: 0
- Low findings: 2 (F-1, F-2)
- Informational findings: 2 (F-3, F-4)

Stack is approved for continued use. Remediation items have been queued in the relevant inboxes.
