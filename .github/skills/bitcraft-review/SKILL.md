---
created_at: 2026-09-17
edited_at: 2026-09-21
creator: ai-ops
editor: ai-ops
ai_gen: true
name: bitcraft-review
description: "Phase 3 entry skill: Review. Stage A checks drift against the original request and in-flight decisions, then removes stale code. Stage B runs a strict style and pattern review (Clean Architecture, strong typing, naming, minimal STE comments, optimality first). Use after implementation, before human review, for PR-style review, and for pre-merge checks."
argument-hint: "[branch, plan path, or changed files]"
---

# Bitcraft Review (Phase 3 Entry)

Entry skill for the Review phase. Runs primarily inside Lead Dev. Findings first, prose second.

## Stage A: Drift and Staleness

1. Read the original request from the plan goal and the in-flight decisions from the plan notes and inbox items.
2. Compare the diff against the request. List every behavior that was added, removed, or changed beyond the request.
3. Check for stale code: dead types, unused fetches, orphaned state, superseded helpers, commented-out blocks.
4. Fix drift and staleness in place when the fix is small.
5. If the fix is major work, stop. Route back to Phase 1 (`bitcraft-plan`) after the human commits or branches as directed.

## Stage B: Strict Code Review

Review the changed files against this checklist, in order:

- Clean Architecture dependency direction preserved. Domain and Application never reference Api or Infrastructure.
- No Domain entities exposed through API contracts.
- Strong typing where applicable. No `dynamic`, no stringly-typed seams, no object bags.
- Naming conventions: `I` prefix on interfaces, `Async` suffix on async methods, `Request`/`Response` on DTOs, `Snapshot` on version records, `Link` on link tables, `Tutor` prefix on tutor-scoped resources.
- Expected failures use `Result<T>`. The Api layer translates to HTTP.
- Authorization and ownership checks intact.
- Comments: minimal, in STE, only where code is not self-explainable or where dense action hides intent (multiple calls, many short lambdas). Delete bloat comments.
- Optimality first, then readability: prefer the smaller standard pattern. Reduce footprint for management and deployment.
- Test coverage for edge cases and failure paths.
- Skill, agent, and instruction frontmatter validity when `.github` files changed.

## Routing After Review

1. Small fixes found: apply them in place. Re-run the narrowest validation.
2. New work discovered: prepare a re-development prompt that names the exact drifts and gaps. Route to Phase 2 (`bitcraft-develop`).
3. Direction change or major gap: route to Phase 1 (`bitcraft-plan`). The human commits current work first, or creates a new branch.
4. All clear: emit the closeout prompt (see Output).

## Specialist Routing

- CIL Medium or above: route the matching specialist review. Sec Ops for auth, privacy, data, infrastructure. AI Ops for agentic stack.
- Create inbox items for follow-up fixes instead of burying action in prose.

## Output

- Stage A drift list with in-place fixes applied or a re-plan recommendation.
- Stage B findings with file and line evidence.
- The next-step prompt, exactly one of:
  - Re-develop: "Run the `bitcraft-develop` skill. Fix these exact items: <numbered drifts and gaps with file paths>."
  - Re-plan: "Run the `bitcraft-plan` skill for <new goal>. Commit current work first."
  - Closeout: "Work accepted. Update docs via `bitcraft-doc-maintenance`. Human commits, then clean up inboxes."
- Tool and MCP security boundaries respected.

## Output

- Findings first, ordered by severity, with file references.
- Review route.
- Validation gaps.
- Follow-up inbox items.
