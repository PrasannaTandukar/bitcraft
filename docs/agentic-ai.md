# Bitcraft Agentic AI Setup

This document is the human-facing overview for Bitcraft's agentic AI workflow. Operational customizations live under `.github`; this guide explains how to use and maintain them.

## Current Baseline

Bitcraft uses GitHub Copilot in VS Code as the primary agent surface. The stack is deliberately modular:

| Layer | Path | Purpose |
| --- | --- | --- |
| Always-on rules | `.github/copilot-instructions.md` | Project identity, hard boundaries, Clean Architecture rules, human-owned surfaces, and validation expectations. |
| Third-party agent entry | `AGENTS.md` (repo root) and `tutor-studio/AGENTS.md` | [agents.md](https://agents.md/) standard entry point for non-Copilot coding agents. |
| Scoped rules | `.github/instructions/*.instructions.md` | File-pattern rules for backend, contracts, tests, frontend, docs, and agentic customization files. |
| Agent roster | `.github/agents/*.agent.md` and `.github/agents/ROSTER.md` | Departmental roles, tool policy, inbox protocol, and guided handoffs. |
| Prompt entrypoints | `.github/prompts/*.prompt.md` | Lean slash-command workflows: overview, research, plan, develop, review, audits, write, metrics, and compact. |
| Skills | `.github/skills/*/SKILL.md` | Reusable procedures loaded on demand. |
| Agent inboxes | `.github/inboxes/<agent-slug>/inbox.md` | Prioritized per-agent backlog of cross-agent work items. |
| Governance | `.github/agentic-ai/*.md` | Project brief, tooling matrix, developer guide, metrics, model routing, audit templates, and framework review notes. |
| MCP specification | `.github/mcp/bitcraft-mcp-spec.md` | Full MCP design surface (most tools still deferred). |
| MCP minimal server | `mcp-server/` and `.vscode/mcp.json` | Read-only Python MCP server exposing inboxes, roster, governance docs, and a Clean Architecture spot check to compatible clients. Also wires the reference `fetch` and read-only `git` MCP servers via `uvx`. |
| Static linting | `.markdownlint.jsonc`, `.vale.ini`, `.github/vale/styles/` | Markdown/prose checks for docs and agentic governance. Prompt evaluation frameworks are deferred to external tooling, not embedded in this repo. |

The older numbered startup/develop/test/review prompt ladder was replaced with role-based workflows. Startup is now an `/overview` workflow owned by Secretary.

## Human And Agent Cooperation

This is not a pure agentic AI project. Humans and agents cooperate.

Humans own product direction, credentials, branch creation, commits, pushes, merges, releases, and final accountability. Agents gather evidence, plan, implement requested changes, validate, review, document, audit, and leave concise backlog items for each other.

Jira and Bitbucket may be used for work management, but credentials and active integration settings stay outside the repository.

## Agent Departments

The active roster is intentionally shorter and clearer than the previous setup.

| Department | Agent | Use for |
| --- | --- | --- |
| Admin | Secretary | Overview, timelines, roadmaps, inbox triage, Jira/Bitbucket status, coordination. |
| Admin | Typewriter | READMEs, guides, audit reports, summaries, and developer docs. |
| Planning | Researcher | Repository and external evidence, technical discovery, model/tool comparisons. |
| Planning | Planner | Technical plans, risks, impacted files, validation, Change Impact Level, delegation. |
| Development | Lead Dev | Development ownership, expert delegation, review routing, fallback implementation. |
| Development | NET Dev | .NET backend, Clean Architecture, EF Core, API/Application/Domain/Infrastructure, backend tests. |
| Development | Web Dev | Tutor Studio React/Vite frontend. |
| Integrity | Dev Ops | CI/CD, Docker, deployment, migrations, branch conventions, release workflow, integrity audits. |
| Integrity | Sec Ops | Security, privacy, authorization, data governance, prompt injection, security audits. |
| Integrity | AI Ops | Prompts, agents, instructions, skills, MCP, metrics, model routing, agentic audits. |

Development responsibility flows through Lead Dev. Expert-only work can go directly to Dev Ops, Sec Ops, AI Ops, or Typewriter when there is no development orchestration need.

## Workflow Entry Points

| Workflow | Prompt | Primary agent |
| --- | --- | --- |
| Work overview | `.github/prompts/overview.prompt.md` | Secretary |
| Discovery/research | `.github/prompts/research.prompt.md` | Researcher |
| Technical planning | `.github/prompts/plan.prompt.md` | Planner |
| Implementation | `.github/prompts/develop.prompt.md` | Lead Dev |
| Impact-aware review | `.github/prompts/review.prompt.md` | Lead Dev |
| Security audit | `.github/prompts/audit-security.prompt.md` | Sec Ops |
| Integrity audit | `.github/prompts/audit-integrity.prompt.md` | Dev Ops |
| Agentic audit | `.github/prompts/audit-agentic.prompt.md` | AI Ops |
| Documentation/report writing | `.github/prompts/write.prompt.md` | Typewriter |
| Metrics review | `.github/prompts/metrics.prompt.md` | AI Ops |
| Session compaction | `.github/prompts/compact.prompt.md` | Secretary |

Typical flow:

1. Secretary gets overview.
2. Researcher runs discovery when evidence is needed.
3. Planner creates a plan and risk classification.
4. Lead Dev delegates implementation.
5. Review is routed by Change Impact Level and affected domain.
6. Typewriter documents approved facts.
7. Secretary or AI Ops tracks metrics and follow-up.

## Inbox System

Agent inboxes are repository-backed prioritized backlogs under `.github/inboxes/<agent-slug>/inbox.md`.

Use inboxes for concise future action only:

- Cross-agent backlog items.
- Follow-up tasks that should survive chat context loss.
- Low-priority parked work that can be batched later.
- Blocked items with a concrete unblocking action.

Do not use inboxes for research reports, permanent logs, secrets, credentials, dataset paths, model weights, or final human-owned deliverable prose. Completed items should be removed rather than accumulated.

The inbox system is separate from VS Code custom-agent UI handoffs. UI handoffs help move the current conversation to another agent; inboxes preserve future work across sessions.

## Metrics And Risk

The metrics system is defined in `.github/agentic-ai/METRICS.md`.

Current risk metrics include:

- Change Severity Score (CSS).
- Dependency Impact Score (DIS).
- Change Impact Level (CIL).
- Change Success Rate (CSR).
- Incident Leakage Rate (ILR).
- DORA-style delivery stability metrics when release data exists.

Agent execution metrics include plan adherence, tool selection accuracy, unsupported claim rate, loop count, token/cost estimates, validation pass rate, rework rate, context loss events, and inbox throughput.

Metrics are guidance until humans approve thresholds and capture location. The current open decisions are listed in `.github/agentic-ai/METRICS.md` and assigned to Secretary's inbox.

## Audits

Recurring audit templates live in `.github/agentic-ai/AUDIT_TEMPLATES.md`.

- Sec Ops owns Security Audits.
- Dev Ops owns Integrity Audits.
- AI Ops owns Agentic Audits.

Audit findings should become prioritized inbox or Jira items. Critical findings are escalated to the human immediately.

## Branching Convention

A Bitbucket/Jira-compatible branch convention is planned but not yet approved. Dev Ops owns the proposal and has an active inbox item for it.

The direction to evaluate is short-lived branches with Jira issue keys and CIL-aware review gates. Agents must not create branches, commits, pushes, merges, or releases.

## Model Routing

The stack supports model families rather than hard-coded model names in frontmatter.

- GPT and Claude families are the main coding, review, planning, and architecture route.
- Gemini is a good fit for deep research and writing-heavy synthesis when available.
- Local Ollama models, including DeepSeek/Qwen on the RTX 3090 setup, are future candidates for low-risk summaries, drafting, batch cleanup, and a later Junior Dev role.

Exact model names should be selected in the active client only after availability, cost, privacy, and quality are verified.

## MCP Security

Two of the three MCP servers wired in `.vscode/mcp.json` have open security findings that require specific handling until Sec Ops completes its sign-off. The third (`bitcraft-mcp`) is read-only and scoped; no action needed.

### `fetch` — Prompt Injection Risk

**What it does:** Fetches any URL and returns the full page content into agent context.

**The risk:** A web page with adversarial embedded text can instruct the agent to perform unintended actions — leaking file contents, generating unwanted code, or chaining unexpected tool calls.

**Required until Sec Ops signs off:**
- In VS Code, open **Settings → Configure Tools** and set `fetch` to **per-request approval only**.
- Never pre-approve `fetch` for an entire session.
- Treat all content returned by `fetch` as untrusted.

**What happens if you skip this:** An agent doing research can load a malicious page and execute injected instructions. The agent may have broad file-read access during a research session, making this a real data-leakage and unintended-action risk.

### `git` — Unconfirmed Write-Capable Tools

**What it does:** Provides Git history, log, diff, and blame access for the workspace.

**The risk:** `mcp-server-git` may also expose write-capable tools (`git_create_branch`, `git_checkout`, `git_commit`, `git_push`) depending on the version installed. These have not been audited on this installation.

**Required until Sec Ops signs off:**
- In VS Code, open **Settings → Configure Tools** and approve only: `git_log`, `git_diff`, `git_show`, `git_blame`, `git_read_file`.
- Actively decline any other `git_*` tools when prompted.
- Do not grant blanket session approval to the `git` server.

**What happens if you skip this:** An agent with unchecked `git` tool access may commit, branch, or push — bypassing the rule that humans own all Git write operations and creating unreviewed history.

Full technical detail and long-term remediation options are in `.github/mcp/bitcraft-mcp-spec.md` under **Known Open Security Findings**.

## Maintenance

Validate `.github` and docs changes with:

- `npx markdownlint-cli2 --config .markdownlint.jsonc ".github/**/*.md" "docs/**/*.md" "*.md"`.
- `vale --config .vale.ini .github docs *.md`.
- Markdown diagnostics.
- Prompt frontmatter: `description`, `agent`, and `argument-hint`.
- Instruction frontmatter: `applyTo`.
- Skill folder/name matching and lowercase skill names.
- Agent frontmatter, tool policy, and stale-name searches.
- Internal consistency between `.github/agentic-ai`, prompts, agents, skills, and this document.

Review the stack when VS Code or Copilot customization syntax changes, workflows drift, new MCP tools are proposed, metrics create bad incentives, or agent roles start overlapping again.

For prompt-stack maintenance methodology and external evaluation options, see `.github/agentic-ai/PROMPT_STACK_MAINTENANCE.md`.
