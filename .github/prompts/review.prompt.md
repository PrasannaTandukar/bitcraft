---
description: 'Run an impact-aware Bitcraft review routed by Change Impact Level and affected domain.'
agent: 'Lead Dev'
argument-hint: '[diff, files, branch, task, changed area, or completed implementation]'
---

# Review

Use this after implementation or before human review.

1. Read `.github/agentic-ai/METRICS.md` and classify or confirm CSS, DIS, and CIL.
2. Review findings first: correctness, Clean Architecture, security/privacy, tests, validation, maintainability, docs, and instruction drift.
3. Route specialist review when needed: Sec Ops for auth/privacy/data, Dev Ops for deployment/infrastructure, AI Ops for agentic stack, NET Dev or Web Dev for implementation details.
4. Create inbox items for follow-up fixes instead of burying action in prose.

Output: findings ordered by severity, review route, validation gaps, and follow-up inbox items.
