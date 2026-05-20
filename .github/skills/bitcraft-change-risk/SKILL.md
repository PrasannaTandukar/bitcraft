---
name: bitcraft-change-risk
description: 'Classify Bitcraft change risk using CSS, DIS, CIL, CSR, and incident leakage concepts. Use before planning, review, audits, branching decisions, and validation routing.'
argument-hint: '[change description, diff, plan, or review scope]'
---

# Bitcraft Change Risk

Use this skill to classify impact before implementing, reviewing, or auditing a change.

## Procedure

1. Read `.github/agentic-ai/METRICS.md`.
2. Identify the changed behavior, data, users, modules, deployment surfaces, and security/privacy boundaries.
3. Assign Change Severity Score (CSS) from 0-10.
4. Assign Dependency Impact Score (DIS) from 0-10.
5. Derive Change Impact Level (CIL): Low, Medium, High, or Critical.
6. Apply overrides for security, privacy, irreversible data, production deployment, credentials, or Git history risk.
7. Choose review and validation route from `.github/agentic-ai/DEVELOPER_GUIDE.md`.

## Output

- CSS and rationale.
- DIS and rationale.
- CIL and overrides.
- Required review route.
- Required validation strength.
- Human decisions or escalations.
