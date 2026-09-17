---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: ai-ops
ai_gen: true
---
# Bitcraft Governance

Semi-locked doctrine, decision logs, and operational reports. Markdown files here are AI-modifiable per their `ai_gen` frontmatter flag; human-managed configuration data belongs in [`.github/config/`](../config/README.md) (D-8).

## Files

- `PROJECT_BRIEF.md` — project framing, goals, scope.
- `DEVELOPER_GUIDE.md` — agentic operating model, workflow, expectations.
- `METRICS.md` — CSS, DIS, CIL internal routing signals only.
- `MODEL_ROUTING.md` — model selection policy (GPT, Claude, Gemini, local Ollama).
- `TOOLING_MATRIX.md` — agent → tool authority matrix.
- `BRANCH_CONVENTION.md` — branch-derived plan-slug rule (D-3); protected branches halt.
- `HUMAN_DECISIONS.md` — append-only log of human policy decisions (D-1, D-2, …).
- `CONTENT_EXCLUSION.md` — content-exclusion mechanisms and limits.
- `PROMPT_STACK_MAINTENANCE.md` — drift-prevention rules for the agentic stack.
- `PROVENANCE.md` — frontmatter standard and provenance requirements.

## See Also

- `.github/config/` — YAML configuration (allow-lists, CIL protocols).
- `.github/agents/ROSTER.md` — department roster.
- `.github/inboxes/` — per-agent prioritized backlog.
- `.github/plans/` — active task plan folders.
