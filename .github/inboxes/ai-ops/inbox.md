---
created_at: 2026-05-19
edited_at: 2026-05-20
creator: ai-ops
editor: ai-ops
ai_gen: true
---
# AI Ops Inbox

- Date: 2026-05-18
- Status: blocked
- Blocked by: Repo memory files are stored in VS Code extension storage outside the workspace; only a human or agent with memory-tool access can refresh them. Re-confirmed blocked 2026-05-20 during feature-freeze v3.1 inbox sweep — AI Ops cannot self-unblock this item.
- Source: AI Ops (Agentic Audit F-2)
- Target: ai-ops
- Priority: medium
- Task: Refresh `/memories/repo/*.md` entries to reflect the current layout: `handoffs`?`inboxes`, root `AGENTS.md` added, MCP server implemented, `MODEL_ROUTING.md` added, `.github/config/` added, `.github/plans/` `.github/audits/` `.github/research/` `.github/reviews/` added with archive+templates, METRICS.md simplified to CSS/DIS/CIL only.
- Acceptance/Cleanup: Repo memory mirrors the current tree; remove this item.

- Date: 2026-05-19
- Status: active
- Source: AI Ops (Agentic Audit F-3)
- Target: ai-ops
- Priority: medium
- Task: After human installs MCP requirements, verify the `bitcraft-mcp` server starts in VS Code and all tools respond, including the new `list_active_plans` and `read_plan` tools added in Batch 2.
- Context: `python -m pip install -r mcp-server/requirements.txt` then VS Code reload. Wiring lives in `.vscode/mcp.json`. Local smoke test on 2026-05-19 passed for all tools.
- Acceptance/Cleanup: All tools respond as expected in VS Code; remove this item or convert any defects into new inbox items.

- Date: 2026-05-19
- Status: active
- Source: AI Ops (Batch 2 follow-up)
- Target: ai-ops
- Priority: medium
- Task: Sweep `.github/` for ambiguity and redundancy now that the new layout is live. Targets: vague verbs ("handle", "improve", "support") without acceptance criteria; duplicate ownership statements across `copilot-instructions.md`, `DEVELOPER_GUIDE.md`, `agents/*.agent.md`, and `skills/*/SKILL.md`; stale paths in active docs; orphan references to removed METRICS sections (CSR, ILR, DORA, change-record schema).
- Context: Use `.github/governance/PROMPT_STACK_MAINTENANCE.md` Ambiguity Controls. Out of scope: archived files inside `archive/` directories.
- Acceptance/Cleanup: Findings fixed in place or queued as new follow-up items. Remove this item.

- Date: 2026-05-19
- Status: active
- Source: AI Ops (D-8 follow-up)
- Target: ai-ops
- Priority: low
- Task: Audit `.github/governance/` for any other human-managed list or config currently stored in Markdown that should be migrated to YAML per D-8 (Markdown = AI-modifiable reports; YAML = human-managed configuration lists).
- Context: First application is `approved-external-sources.yaml`. Candidates to evaluate: any other allow-lists, model rosters, or env-style settings under `.github/governance/`. Do not migrate doctrine/report Markdown.
- Acceptance/Cleanup: Migration plan or "none required" finding documented; remove this item.

- Date: 2026-05-19
- Status: active
- Source: Planner (Batch 2 WS7)
- Target: ai-ops
- Priority: low
- Task: Run `audit-agentic` after this batch settles to confirm CIL protocols, METRICS strip, new MCP tools, and updated prompts remain internally consistent.
- Context: See `.github/plans/batch-2-agentic-stack/plan.md` WS7. Output report goes to `.github/audits/AUDIT_<DATE>_AGENTIC.md` using `.github/audits/templates/audit.template.md`.
- Acceptance/Cleanup: Audit report produced; findings converted to inbox items or fixed inline; remove this item.

- Date: 2026-05-18
- Status: parked
- Source: AI Ops
- Target: ai-ops
- Priority: low
- Task: Schedule the next Agentic Audit after the branch convention is approved or a material primitive changes.
- Context: Use `.github/audits/templates/audit.template.md`. Reference precedent in `.github/audits/archive/AUDIT_2026-05-18_AGENTIC.md`.
- Acceptance/Cleanup: New audit report file is produced and findings converted to inbox items; remove this item afterward.
