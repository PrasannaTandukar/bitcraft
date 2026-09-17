---
created_at: 2026-09-17
edited_at: 2026-09-17
creator: ai-ops
editor: ai-ops
ai_gen: true
name: bitcraft-work-management
description: 'Manage Bitcraft human-agent work: session overview, triage, task breakdown, roadmaps, inbox operations (create, prune, block, delegate), and session compaction. Use for "what should happen next", status summaries, backlog hygiene, cross-agent routing, and preserving durable context at session end.'
argument-hint: "[idea, feature, roadmap, inbox state, work overview, or session to compact]"
---

# Bitcraft Work Management

Use this skill to keep human-agent development organized and auditable. Runs primarily inside the Secretary agent.

## When to Use This Skill

- The user asks for a work overview, roadmap, status, or "what next".
- The user asks to create, prune, block, park, or delegate inbox items.
- A session has accumulated context that must survive as inbox items (compaction).
- Multiple agents or work streams need coordination.

## Overview Procedure

1. Read `.github/copilot-instructions.md`, `.github/governance/PROJECT_BRIEF.md`, and `.github/governance/DEVELOPER_GUIDE.md`.
2. Read `.github/inboxes/secretary/inbox.md` and scan the other inboxes when cross-team state matters.
3. Summarize active work, blockers, stale items, and human decisions needed.
4. Recommend the next route: Planner, Lead Dev, Sec Ops, or AI Ops.
5. Update the Secretary inbox only when there is a durable coordination item.

## Inbox Rules

- Local inboxes can live at `.github/inboxes/<agent-slug>/inbox.md` for the five agents: secretary, planner, lead-dev, sec-ops, ai-ops. Git ignores this folder.
- Use inboxes for short-lived future action, not current chat narration or durable project state.
- One item = one concrete Task sentence, minimum safe Context with file links, Priority, Acceptance/Cleanup criteria.
- Priority: `high` = blocks the current user goal or has a this-week deadline; `medium` = required for the next planned milestone; `low` = batchable backlog.
- Remove completed items. Mark blocked items with the next human or agent action.
- Put summaries, decisions, and reports in tracked plans, `.github/governance/`, `.github/audits/`, `.github/research/`, `.github/reviews/`, or `docs/`, not inboxes.

## Compaction Procedure

Use when the conversation has accumulated context that should survive but does not need replay.

1. Identify the current goal, verified completed work, and explicit follow-ups. If unclear, ask the user to confirm scope. Do not invent goals.
2. Group remaining work by owning agent per `.github/agents/ROSTER.md`.
3. Draft one inbox item per concern using the inbox rules above. Do not duplicate existing items.
4. Append to the target local inbox. Move any durable result to a tracked document. Remove transient session chatter from any inbox touched.
5. Report what was kept, dropped, and which inboxes changed.

Safety rules: never copy secrets, credentials, raw datasets, model weights, or full transcripts into an inbox. If an item has no clear owner, queue it on the Secretary inbox with `Status: parked`.

## Handoff Pattern

- Secretary -> `bitcraft-research` skill when discovery or external docs matter.
- Secretary -> Planner when a decision needs implementation structure.
- Planner -> Lead Dev for development work (backend and frontend).
- Lead Dev -> Sec Ops for security/infrastructure review, -> AI Ops for agentic stack work.
- Audit owners (Sec Ops, AI Ops) -> inbox items for iterative remediation.

## Gotchas

- Do not let a plan become an unverified claim about current repository state.
- Do not keep planning after implementation is explicitly requested.
- Do not let inboxes become historical logs.
