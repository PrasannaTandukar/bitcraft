# Bitcraft Agentic Architecture Reference

> **This document is a high-level pointer only.** The detailed, authoritative content has moved:
>
> - Architecture principles, department model, and workflow: [`.github/agentic-ai/PROJECT_BRIEF.md`](../.github/agentic-ai/PROJECT_BRIEF.md) and [`.github/agentic-ai/DEVELOPER_GUIDE.md`](../.github/agentic-ai/DEVELOPER_GUIDE.md).
> - Human-facing overview, security notes, and maintenance guide: [`docs/agentic-ai.md`](agentic-ai.md).
> - Portable setup template for new projects: [`docs/agentic-setup-guide.md`](agentic-setup-guide.md).
>
> Do not update the sections below \u2014 they are kept only for historical context and will not be kept in sync.

---

## Principles

| Principle | Bitcraft interpretation |
| --- | --- |
| Human-agent cooperation | Humans own product decisions, credentials, Git write operations, releases, and final accountability. Agents support the workflow. |
| Role isolation | Each agent has one primary job, a narrow tool policy, and an inbox. |
| Artifact separation | Instructions, agents, prompts, skills, inboxes, MCP specs, governance docs, and human docs are separate layers. |
| Measured improvement | Change risk, delivery outcome, agent execution, model routing, and inbox health are measured when they improve decisions. |
| Lean by default | Prefer fewer agents, focused prompts, and reusable skills over duplicating policy everywhere. |

## File Structure

```text
.github/
├── copilot-instructions.md
├── agents/
│   ├── ROSTER.md
│   ├── secretary.agent.md
│   ├── researcher.agent.md
│   ├── planner.agent.md
│   ├── lead-dev.agent.md
│   ├── net-dev.agent.md
│   ├── web-dev.agent.md
│   ├── dev-ops.agent.md
│   ├── sec-ops.agent.md
│   ├── ai-ops.agent.md
│   └── typewriter.agent.md
├── prompts/
│   ├── overview.prompt.md
│   ├── research.prompt.md
│   ├── plan.prompt.md
│   ├── develop.prompt.md
│   ├── review.prompt.md
│   ├── audit-security.prompt.md
│   ├── audit-integrity.prompt.md
│   ├── audit-agentic.prompt.md
│   ├── write.prompt.md
│   ├── metrics.prompt.md
│   └── compact.prompt.md
├── skills/
│   └── <skill-name>/SKILL.md
├── instructions/
│   └── *.instructions.md
├── inboxes/
│   ├── README.md
│   └── <agent-slug>/inbox.md
├── agentic-ai/
│   ├── PROJECT_BRIEF.md
│   ├── TOOLING_MATRIX.md
│   ├── DEVELOPER_GUIDE.md
│   ├── METRICS.md
│   ├── MODEL_ROUTING.md
│   ├── AUDIT_TEMPLATES.md
│   └── FRAMEWORK_REVIEW.md
└── mcp/
    └── bitcraft-mcp-spec.md
```

The repo root also carries `AGENTS.md` per the [agents.md standard](https://agents.md/) for third-party coding agent compatibility. Nested `AGENTS.md` may appear in subprojects (for example `tutor-studio/AGENTS.md`).

Human-facing documentation lives in `docs/`. Operational AI assets live in `.github/`.

## Department Model

Bitcraft uses 10 agents grouped into four departments.

- Admin: Secretary and Typewriter.
- Planning: Researcher and Planner.
- Development: Lead Dev, NET Dev, and Web Dev.
- Integrity: Dev Ops, Sec Ops, and AI Ops.

Lead Dev is the development captain: delegate first when expertise is clear, implement directly when the slice is small or cross-cutting, and route review by risk.

AI Ops aggregates prompt engineering, MCP design, skill maintenance, agent design, model routing, and metrics so those concerns do not sprawl across separate agents.

## Workflow

```text
Human request
  -> Secretary overview when state is unclear
  -> Researcher when evidence is needed
  -> Planner for technical plan and risk classification
  -> Lead Dev for development ownership
      -> NET Dev / Web Dev / Dev Ops / Sec Ops / AI Ops as needed
  -> Review route based on Change Impact Level
  -> Typewriter for approved docs and reports
  -> Secretary or AI Ops for metrics and follow-up inbox hygiene
```

Expert-only work can bypass Lead Dev when no development orchestration is needed. For example, Sec Ops can run a security audit directly, and AI Ops can maintain `.github` directly.

## Inbox System

The inbox system is a repository-backed coordination queue. It is intentionally simple markdown rather than a permanent database.

Each inbox item includes date, status, source, target, priority, task, context, and acceptance/cleanup. Completed items are removed. Blocked items state who or what unblocks them. Parked items are valid future work that should not drive the current turn.

Inboxes are best for:

- Cross-agent backlog.
- Deferred follow-up.
- Audit remediation.
- Batchable low-priority tasks.
- Human decisions that must be collected later.

Inboxes are not for:

- Secrets or credentials.
- Long research reports.
- Permanent activity logs.
- Final human-owned deliverable prose.
- Dataset, model-weight, or private local path references.

## Metrics-Based Decisions

The metrics system is owned by AI Ops and documented in `.github/agentic-ai/METRICS.md`.

Risk routing starts with:

- CSS: Change Severity Score.
- DIS: Dependency Impact Score.
- CIL: Change Impact Level.

Outcome metrics include:

- CSR: Change Success Rate.
- ILR: Incident Leakage Rate.
- DORA-style delivery metrics where release data exists.

Agent/model metrics include plan adherence, tool selection accuracy, unsupported claim rate, loop count, token/cost estimate, validation pass rate, rework rate, context loss, and inbox throughput.

Metrics are not approved as hard gates yet. The current stack records definitions and decision questions first; Secretary and Dev Ops own the next human-facing branch/metrics decisions.

## Audits

The architecture has three recurring audit lanes:

- Security Audit by Sec Ops.
- Integrity Audit by Dev Ops.
- Agentic Audit by AI Ops.

All audits use `.github/agentic-ai/AUDIT_TEMPLATES.md`. Findings should be converted into inbox or Jira items, not left as untracked report prose.

## Branching And Work Management

Branching conventions are planned work, not implemented policy. Dev Ops has an active inbox item to propose a Bitbucket/Jira-compatible convention.

The preferred direction to evaluate is short-lived branches with Jira issue keys and risk-aware review gates. Agents do not create branches, commits, pushes, merges, tags, or releases.

## Model Families

The framework is model-family aware:

- GPT and Claude families for primary coding, planning, architecture, and review.
- Gemini for deep research and writing-heavy synthesis where available.
- Local Ollama DeepSeek/Qwen family models for future low-risk local workflows and possible Junior Dev experimentation.

Exact model names are kept out of frontmatter until verified in the active Copilot client.

## Anti-Overengineering Guardrails

- Do not add an agent when a skill or prompt is enough.
- Do not add a prompt when an existing workflow already routes the work.
- Do not duplicate long policy text across prompts, agents, and skills.
- Do not turn inboxes into logs.
- Do not automate metrics capture before the capture location and thresholds are approved.
- Do not make MCP write tools default.
- Do not create long-lived agent branches.

## Current Open Decisions

- Human-approved branching convention for Bitbucket/Jira.
- Metrics capture location and thresholds.
- Definition of agent-caused incident for CSR and ILR.
- Whether token/cost data is manual, dashboard-based, or deferred.
- When to trial a local Junior Dev model.
