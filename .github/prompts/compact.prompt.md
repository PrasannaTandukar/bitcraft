---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
description: 'Compact the current session into prioritized inbox items per impacted agent, preserving only durable context.'
agent: 'Secretary'
argument-hint: '[session focus or specific items to compact]'
---

# Compact Session

Use when the conversation has accumulated context that should survive but does not need to be replayed in the next turn.

## Procedure

### Phase 1: Assess

1. Identify the user's current goal, the verified work completed this session, and the explicit follow-ups.
2. If no clear goal is identified or session context is incomplete, ask the user to confirm scope before proceeding. Do not invent goals.

### Phase 2: Group

3. Group remaining work by owning agent using the roster in `.github/agents/ROSTER.md`. If the roster is missing or inaccessible, queue all items in the Secretary inbox with `Status: parked` and a note for human triage.

### Phase 3: Draft Items

4. For each owning agent, draft one inbox item per concern using the schema in `.github/inboxes/README.md`:
   - One concrete Task sentence.
   - Minimum safe Context with file links.
   - Priority using these definitions: `high` = blocks current user goal or has immediate (this-week) deadline; `medium` = required for the next planned milestone; `low` = nice-to-have or batchable backlog.
   - Acceptance/Cleanup criteria.

### Phase 4: Write

5. Open each target inbox at `.github/inboxes/<agent-slug>/inbox.md` and append the items. Do not duplicate items already present.
6. Remove transient session chatter from any inbox you touched. Do not edit unrelated items.

### Phase 5: Report

7. Report the compaction summary back to the user: what was kept, what was dropped, and which inboxes were updated.

## Rules

Apply in priority order:

1. **Safety first**: Never copy secrets, credentials, raw datasets, dataset paths, model weights, or full transcripts into an inbox.
2. **Format**: Condense actionable information into concise, bullet-pointed tasks without including descriptive or explanatory text.
3. **Routing**: If a concern does not have a clear owning agent, queue it on the Secretary inbox with `Status: parked` and a note for human triage.
4. **Deliverables**: If the session produced a deliverable that belongs in `.github/governance/` or `docs/`, link to it from the relevant inbox item rather than pasting prose. If the compact output itself becomes a standalone summary document, emit the provenance line defined in `.github/governance/PROVENANCE.md`.

## Output

- Inbox files updated.
- Items added per agent.
- Items intentionally dropped and why.
- Items left for the user to triage.
