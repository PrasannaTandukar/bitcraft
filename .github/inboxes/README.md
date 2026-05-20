# Bitcraft Agent Inboxes

Each agent owns a prioritized backlog at `.github/inboxes/<agent-slug>/inbox.md`. Inboxes batch future work so an agent can pick it up the next time it is activated, and so other agents (and humans) can drop work onto a role without interrupting current execution.

## Purpose

- **Batching**: an agent activated for a task reads its own inbox first, then handles the highest-priority item that fits the current request, and queues the rest.
- **Cross-agent delegation**: any agent can add items to another agent's inbox to request future action without seizing control of the current turn.
- **Aggregation**: Secretary scans all inboxes to summarize cross-team backlog and surface overdue or conflicting items.
- **Continuity**: items survive session compaction and model restarts because they live in the repository.

Inboxes are not logs, transcripts, audit reports, framework documentation, or long-term memory. Detailed deliverables belong in `.github/agentic-ai/` or `docs/`.

## Item Schema

```markdown
- Date: YYYY-MM-DD
- Status: active | blocked | parked
- Source: <agent or Human>
- Target: <agent-slug>
- Priority: high | medium | low
- Task: One concrete actionable sentence.
- Context: Minimum safe context and links to owning files or docs.
- Acceptance/Cleanup: Completion check and whether to remove or update this item.
```

## Priority Rules

- **high**: pull into the current session if it overlaps the active request, otherwise schedule next.
- **medium**: handle when an agent is next activated for its role.
- **low**: parked backlog; review during overview or audit, may be deferred or dropped.

If a high-priority item conflicts with the user's current request, finish the user's request first and either keep the high item or escalate to the user.

## Status Rules

- **active**: ready to be worked.
- **blocked**: needs a specific unblocking action stated in the item.
- **parked**: valid future work that should not drive current activity.

## Lifecycle

1. On activation: read your own inbox before planning role-specific work.
2. During work: pull at most one high-priority unrelated item into the current turn only if it is low-risk and the user is informed.
3. On finish: remove completed items, update blocked items with the unblocking action, leave parked items in place.
4. Cross-agent delegation: add an item to the target agent's inbox with a clear Task and minimum safe Context. Do not duplicate the same item across multiple inboxes.
5. Aggregation: Secretary can produce a cross-inbox summary but must not edit other agents' items without that agent's role authority.

## Rules

- Keep items short. Link to docs rather than copy them.
- One item per concern. Split if a sentence does not capture the task.
- Never store secrets, credentials, dataset paths, model weights, private local paths, or final human-authored deliverable prose.
- Do not let inboxes drift into chat history. Prune on a regular cadence.
