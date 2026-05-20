---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
description: 'Get a Bitcraft work overview through Secretary: active goals, inbox items, blockers, Jira/Bitbucket considerations, and recommended next route.'
agent: 'Secretary'
argument-hint: '[optional focus area, such as backend, frontend, agentic stack, audit, Jira, or release]'
---

# Work Overview

Use this when starting a session, returning after drift, or asking what should happen next.

1. Read `.github/copilot-instructions.md`, `.github/governance/PROJECT_BRIEF.md`, `.github/governance/DEVELOPER_GUIDE.md`, and `.github/inboxes/secretary/inbox.md`.
2. Inspect inbox items, governance docs, and the specific surface area matching the requested focus.
3. Summarize active work, blockers, stale inbox items, and human decisions needed.
4. Recommend the next route: Researcher, Planner, Lead Dev, Dev Ops, Sec Ops, AI Ops, or Typewriter.
5. Update Secretary inbox only when there is a durable coordination item.

Output: current state, active inbox items, recommended route, and human decisions needed.
