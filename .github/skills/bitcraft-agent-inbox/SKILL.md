---
name: bitcraft-agent-inbox
description: 'Use and maintain Bitcraft agent inboxes as prioritized per-agent backlogs. Use when reading, creating, pruning, blocking, parking, or delegating cross-agent backlog items under .github/inboxes.'
argument-hint: '[agent slug, inbox item, delegation need, or cleanup task]'
---

# Bitcraft Agent Inbox

Use this skill whenever an agent needs durable cross-agent coordination.

## Procedure

1. Read `.github/inboxes/README.md` and your own `.github/inboxes/<agent-slug>/inbox.md`.
2. Identify active items relevant to the current task.
3. Act on the smallest item that matches the user's current request and your role.
4. Remove completed items when you have edit authority.
5. Mark blocked items with the concrete human or agent action needed.
6. Park low-priority items only when they should survive but not drive current work.
7. Add new items only for future action by a specific agent.

## Item Rules

- Keep each item concise and actionable.
- Include only the context needed to act safely.
- Link to owning docs or files instead of copying long summaries.
- Put research reports, audit outputs, and framework decisions in `.github/agentic-ai/` or `docs/`.
- Never store secrets, credentials, private local paths, datasets, model weights, or final human-owned prose in an inbox.

## Output

- Items read.
- Items completed, blocked, parked, added, or left unchanged.
- Next owner.
