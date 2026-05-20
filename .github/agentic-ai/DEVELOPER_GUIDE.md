# Bitcraft Agentic Workflow Guide

## Standard Flow

1. Secretary gets work overview, checks relevant inboxes, and surfaces active priorities or blockers. Secretary is **triage**, not orchestration.
2. Researcher gathers evidence when the user needs discovery, external documentation, academic input, or model/tool comparison.
3. Planner converts evidence and requirements into a technical plan with risks, impacted files, validation, and Change Impact Level.
4. Lead Dev accepts development responsibility and acts as the **development orchestrator**, delegating implementation slices to NET Dev, Web Dev, Dev Ops, Sec Ops, or AI Ops.
5. Implementing agents make small validated edits and update their own inboxes when they complete or block delegated work.
6. Review is routed by Change Impact Level and domain: Lead Dev for ordinary code review, Sec Ops for security/privacy risk, Dev Ops for deployment/infrastructure risk, and AI Ops for agentic stack risk.
7. Typewriter turns approved technical facts into presentable docs or audit reports when needed. Standalone AI-generated reports include the provenance line from `.github/agentic-ai/PROVENANCE.md`.
8. Secretary or AI Ops captures metrics and queues follow-up work.

## Inbox Use

- Inboxes live under `.github/inboxes/<agent-slug>/inbox.md`.
- Use inboxes for future action, not current chat narration.
- Keep each item small enough that the target agent can act without rereading the whole conversation.
- Remove completed items. Mark blocked items with the next human or agent action.
- Put summaries, decisions, metrics definitions, and audit reports in `.github/agentic-ai/` or `docs/`, not inboxes.

## Development Delegation

- Lead Dev owns development task routing.
- NET Dev owns backend .NET, EF Core, Application/Domain/Infrastructure/API, and backend tests.
- Web Dev owns `tutor-studio/` frontend work.
- Dev Ops owns Docker, CI/CD, deployment, migrations, release mechanics, and branch policy proposals.
- Sec Ops owns security, privacy, authorization, prompt-injection, model/tool approval, and data governance risk.
- AI Ops owns `.github`, MCP specs, prompts, agents, skills, model-routing docs, and agentic metrics.

## Review Routing By Impact

Use `.github/agentic-ai/METRICS.md` before review.

| Change Impact Level | Minimum review route |
| --- | --- |
| Low | Implementing agent self-check plus focused validation. |
| Medium | Lead Dev or relevant expert review. |
| High | Lead Dev plus affected expert; Sec Ops for auth/privacy/data, Dev Ops for deployment/infrastructure, AI Ops for agentic stack. |
| Critical | Human escalation before implementation or merge; Sec Ops and Dev Ops review when applicable. |

## Branching Work

The next operational task is a Bitbucket/Jira-compatible branch convention. Dev Ops owns the proposal, but humans approve the policy and perform Git write operations.

Preferred direction to evaluate:

- Trunk-based or GitHub-flow style with short-lived branches.
- Branch names include Jira issue keys when available.
- Branches encode work type and risk without creating long-lived agent branches.
- High-impact or critical changes require explicit human approval before branch creation and merge.

## Metrics Capture

Capture metrics only when they improve decisions. Use trends and retrospectives, not single-number targets. Start manually or with Jira/Bitbucket fields before adding automation.

Minimum capture per AI-assisted change:

- Agent and model family used.
- Prompt or workflow entrypoint.
- Change Severity Score, Dependency Impact Score, Change Impact Level.
- Validation run and result.
- Review route.
- Rework, incident leakage, or blocked state.

## Drift Control

- If a workflow fails repeatedly, update the owning prompt, skill, or agent instead of adding duplicate warnings everywhere.
- If a metric produces bad incentives, revise the metric before using it for routing.
- If an inbox becomes a log, have Secretary or AI Ops prune it back to active items.

## Companion Documents

- `.github/agentic-ai/PROVENANCE.md` — convention for marking AI-generated standalone reports.
- `.github/agentic-ai/CONTENT_EXCLUSION.md` — how Copilot exclusion actually works in this repo (no `.copilotignore`).
- `.github/agentic-ai/MODEL_ROUTING.md` — task categories, agent-to-family routing, hybrid cascade.
- `.github/agentic-ai/PROMPT_STACK_MAINTENANCE.md` — lean maintenance loop, ambiguity controls, static checks, and external evaluation options.
- `.github/mcp/bitcraft-mcp-spec.md` — MCP design surface, including which third-party MCPs are wired in and which are out of scope.
- `.markdownlint.jsonc`, `.vale.ini`, `.github/vale/styles/` — static Markdown/prose checks for the agentic and docs surface.
