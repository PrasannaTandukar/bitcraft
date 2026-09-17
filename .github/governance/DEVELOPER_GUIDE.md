---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# Bitcraft Agentic Workflow Guide

## Standard Flow

1. Secretary gets work overview, checks relevant inboxes, and surfaces active priorities or blockers. Secretary is **triage**, not orchestration, and also produces documentation and reports.
2. Research runs as the `bitcraft-research` skill inside Secretary or Planner when the user needs discovery, external documentation, academic input, or model/tool comparison.
3. Planner converts evidence and requirements into a technical plan with risks, impacted files, validation, and Change Impact Level.
4. Lead Dev accepts development responsibility and implements directly — backend (.NET) and frontend (React/Vite) both live in this role.
5. Lead Dev makes small validated edits and updates its inbox when work completes or blocks.
6. Review is routed by Change Impact Level and domain: Lead Dev for ordinary code review, Sec Ops for security/privacy/deployment risk, and AI Ops for agentic stack risk.
7. Secretary turns approved technical facts into presentable docs or audit reports when needed. Standalone AI-generated reports include the provenance line from `.github/governance/PROVENANCE.md`.
8. Secretary or AI Ops captures metrics and queues follow-up work.

## Inbox Use

- Inboxes live under `.github/inboxes/<agent-slug>/inbox.md`.
- Use inboxes for future action, not current chat narration.
- Keep each item small enough that the target agent can act without rereading the whole conversation.
- Remove completed items. Mark blocked items with the next human or agent action.
- Put summaries, decisions, metrics definitions, and audit reports in `.github/governance/` or `docs/`, not inboxes.
- Keep inbox items concise. Do not use inboxes as activity logs; noisy inbox churn creates version-control noise without improving coordination.

## Markdown Metadata

- Every tracked Markdown file carries `created_at`, `edited_at`, `creator`, `editor`, and `ai_gen` frontmatter.
- `ai_gen: true` is the default for agent-generated documents. `ai_gen: false` is the default for human-created documents.
- Humans may edit any document and are the only ones who may change `ai_gen` after creation.
- Agents may update `edited_at` and `editor` when they make approved edits. Agents must not change `created_at` after initial backfill.
- Vale is the enforcement surface for this iteration. Do not add scripts, hooks, or CI gates unless a later human decision approves them.

## Documentation Lookup

Normal documentation lookup uses this fallback hierarchy:

1. Consult local repository docs, governance docs, and developer-local `docs/dev/` stacks.
2. Use approved external documentation retrieval only when the human explicitly grants it.
3. Use broader web retrieval only for deep research, and collect sources.
4. Delegate to the human when sources are absent, conflicting, inaccessible, or require approval.

Deep research is expected to run steps 1 through 3 when available and report local/remote source discrepancies. Local developer docs under `docs/dev/` are human-provided inputs, not product Documents/Knowledge ingestion.

## Developer-Local Documentation Stacks

- `docs/dev/` is ignored by Git and populated locally by humans or developers.
- Use `docs/dev/<stack-slug>/STACK.md` for stack metadata and instructions.
- Put human-provided source files under `docs/dev/<stack-slug>/sources/`.
- Agents may read these stacks when present, but must tolerate the folder being absent or empty.

## Development Ownership

- Lead Dev owns development execution end to end: backend .NET, EF Core, Application/Domain/Infrastructure/API, backend tests, and `tutor-studio/` frontend work.
- Sec Ops owns security, privacy, authorization, prompt-injection, model/tool approval, data governance risk, plus CI/CD, Docker, deployment, migrations, release mechanics, and branch policy proposals.
- AI Ops owns `.github`, MCP specs, prompts, agents, skills, model-routing docs, and agentic metrics.
- Secretary owns coordination, documentation, and reports.
- Domain-specific how-to lives in skills, not agents: `bitcraft-backend-feature`, `bitcraft-frontend-feature`, `bitcraft-devops-release`, `bitcraft-security-review`, `bitcraft-doc-maintenance`.

## Review Routing By Impact

Use `.github/governance/METRICS.md` to classify CSS, DIS, and CIL. Use the matching protocol file in `.github/config/cil-<level>.yaml` for required agents, research, tools, validation, review, and escalation.

| Change Impact Level | Protocol file | Minimum review route |
| --- | --- | --- |
| Low | `.github/config/cil-low.yaml` | Implementing agent self-check plus focused validation. |
| Medium | `.github/config/cil-medium.yaml` | Lead Dev plus relevant specialist. |
| High | `.github/config/cil-high.yaml` | Lead Dev plus mandatory specialist (Sec Ops or AI Ops). |
| Critical | `.github/config/cil-critical.yaml` | Human pre-approval before any work; Sec Ops mandatory. |

## Document Persistence Layout

Recurring AI-generated documents live in dedicated folders with `archive/` and `templates/` subfolders. MCP excludes `archive/` from default context.

| Folder | Purpose | Templates | Archive |
| --- | --- | --- | --- |
| `.github/plans/<branch-slug>/` | Active task plans (`plan.yaml`, optional `plan.md`/`context.md`) | `.github/plans/templates/` | `.github/plans/archive/` |
| `.github/audits/` | Active audit reports | `.github/audits/templates/` | `.github/audits/archive/` |
| `.github/research/` | Active research deliverables | `.github/research/templates/` | `.github/research/archive/` |
| `.github/reviews/FRAMEWORK_REVIEW.md` | Rolling design log | `.github/reviews/templates/` | `.github/reviews/archive/` |
| `.github/config/` | CIL execution protocols (YAML, no archive) | n/a | n/a |
| `.github/governance/` | Stable governance reference (briefs, metrics, routing, provenance, exclusion, maintenance) | n/a | n/a |

Lifecycle: open → active folder → archive when complete or superseded. Always start from a template under `templates/`.

## Branching Work

The branch convention is captured in `.github/governance/BRANCH_CONVENTION.md` (D-3). Sec Ops maintains the convention guidance and CI/merge-policy compatibility; humans approve the policy and perform Git write operations.

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

- `.github/governance/PROVENANCE.md` — convention for marking AI-generated standalone reports.
- `.github/governance/CONTENT_EXCLUSION.md` — how Copilot exclusion actually works in this repo (no `.copilotignore`).
- `.github/governance/MODEL_ROUTING.md` — task categories, agent-to-family routing, hybrid cascade.
- `.github/governance/PROMPT_STACK_MAINTENANCE.md` — lean maintenance loop, ambiguity controls, static checks, and external evaluation options.
- `.github/mcp/bitcraft-mcp-spec.md` — MCP design surface, including which third-party MCPs are wired in and which are out of scope.
- `.markdownlint.jsonc`, `.vale.ini`, `.github/vale/styles/` — static Markdown/prose checks for the agentic and docs surface.
