---
created_at: 2026-09-17
edited_at: 2026-09-17
creator: ai-ops
editor: ai-ops
ai_gen: true
name: bitcraft-review
description: "Run an impact-aware Bitcraft review routed by Change Impact Level. Covers code, docs, skills, agents, and inboxes: correctness, Clean Architecture, security/privacy, test adequacy, maintainability, and instruction drift. Use after implementation, before human review, for PR-style review, and for pre-merge checks."
argument-hint: "[diff, files, branch, task, changed area, or completed implementation]"
---

# Bitcraft Review

Run a focused review with findings first. Runs primarily inside Lead Dev.

## Procedure

1. Identify the requested scope and changed files. Read enough surrounding code to evaluate behavior, not just syntax.
2. Read `.github/governance/METRICS.md` and classify or confirm CSS, DIS, and CIL using the `bitcraft-change-risk` skill.
3. Prioritize bugs, security issues, architecture violations, test gaps, and maintenance risk.
4. Route specialist review when CIL is Medium or above: Sec Ops for auth/privacy/data/infrastructure, AI Ops for agentic stack.
5. Create inbox items for follow-up fixes instead of burying action in prose.

## Review Checklist

- Clean Architecture dependency direction preserved.
- No Domain entities exposed through API contracts.
- Authorization and ownership checks intact.
- Expected errors represented as `Result<T>`.
- Snapshot and audit integrity.
- Test coverage for edge cases and failure paths.
- Skill/agent frontmatter validity when `.github` files changed.
- Inbox items actionable, current, not historical logs.
- Tool and MCP security boundaries respected.

## Output

- Findings first, ordered by severity, with file references.
- Review route.
- Validation gaps.
- Follow-up inbox items.
