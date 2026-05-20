---
name: bitcraft-review
description: 'Review Bitcraft code, docs, prompts, agents, skills, and inboxes for correctness, security, Clean Architecture, test adequacy, maintainability, instruction drift, and CIL-aware routing. Use for PR-style review, pre-merge checks, and AI customization audits.'
argument-hint: '[diff, changed area, or review goal]'
---

# Bitcraft Review

Run a focused review with findings first.

## When to Use This Skill

- The user asks for review, audit, critique, or quality assessment.
- A change touches Domain/Application/API boundaries, auth, persistence, AI workflows, or `.github` customizations.
- The team needs to detect instruction drift or stale agentic AI files.

## Procedure

1. Identify the requested scope and changed files.
2. Read enough surrounding code to evaluate behavior, not just syntax.
3. Prioritize bugs, security issues, architecture violations, test gaps, and maintenance risk.
4. Classify or confirm CIL from `.github/agentic-ai/METRICS.md` when review depth or expert routing is unclear.
5. Check `.github` customization frontmatter, layering, and inbox hygiene if agentic files changed.
6. Provide findings first, ordered by severity.

## Review Checklist

- Clean Architecture dependencies.
- Domain entity exposure.
- Authorization and ownership checks.
- Expected errors represented as `Result<T>`.
- Snapshot and audit integrity.
- Test coverage for edge cases and failure paths.
- Prompt/agent/skill frontmatter validity.
- Inbox items are actionable, current, and not historical logs.
- Tool and MCP security boundaries.

## Output Contract

- Findings first with file references.
- Open questions.
- Residual risk and test gaps.
- Brief change summary only after issues.
