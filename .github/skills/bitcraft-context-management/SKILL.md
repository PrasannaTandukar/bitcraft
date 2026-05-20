---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
name: bitcraft-context-management
description: 'Manage Bitcraft coding-agent context for long tasks, restarts, handoffs, and prompt compaction. Use when work spans many files, agent memory is stale, research artifacts may be confused with current state, or a session needs a concise evidence-backed working summary.'
argument-hint: '[task, handoff, or current session concern]'
---

# Bitcraft Context Management

Keep the working context accurate, small, and evidence-backed.

## When to Use This Skill

- A task spans multiple agents, prompts, or turns.
- The user mentions memory, context drift, compaction, handoff, or stale assumptions.
- Research markdown files could be mistaken for operational project state.
- A plan or implementation needs a current-state refresh.

## Procedure

1. Identify the active user request and newest override.
2. Separate verified repository state from generated research, previous plans, and memories.
3. Read only the nearest relevant files, tests, prompts, or docs needed to validate the current claim.
4. Record durable repo conventions only when they are verified and likely reusable.
5. Record session state only when it helps a future turn continue the same task.
6. Remove or correct stale session memories when they would mislead future work.

## Output Contract

- Current user goal.
- Verified current state.
- Active constraints.
- Next smallest action.
- Validation path.

## Memory Scopes

The Copilot runtime exposes three memory scopes. Use them deliberately.

- **User memory** (`/memories/`): durable cross-workspace preferences, common commands, and lessons that survive every session. Keep entries short, one fact per line.
- **Session memory** (`/memories/session/`): task-specific working notes for the current conversation. Use for in-progress plans, open questions, and active TODO state. Do not persist what should live in repo memory.
- **Repo memory** (`/memories/repo/`): repository-scoped conventions that apply only in this workspace (current architecture facts, naming conventions, glossary, build commands). Update when the repo's reality changes; remove entries that are now wrong.

Rules:

- Inbox items in `.github/inboxes/<agent-slug>/inbox.md` are the durable cross-agent backlog. Memory is private to the agent runtime.
- Never store secrets, credentials, dataset paths, model weights, or private local paths in any memory scope.
- After a major repository restructure (folder rename, agent roster change, doc rewrite), refresh `/memories/repo/` so stale facts cannot mislead future sessions.

## Compaction

Use `compact.prompt.md` to convert a long session into prioritized inbox items per impacted agent. Compaction reduces context load without losing follow-up work.

## Gotchas

- Do not treat research markdown, action plans, or generated implementation guides as current project state unless the user explicitly promotes them.
- Do not expand context by reading broad surfaces when a local anchor is enough.
- Do not create persistent user memory for one-off task details.
- Do not let session memory bleed into repo memory; keep scope discipline.
