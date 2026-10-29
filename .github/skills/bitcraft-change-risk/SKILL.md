---
created_at: 2026-05-19
edited_at: 2026-09-17
creator: ai-ops
editor: ai-ops
ai_gen: true
name: bitcraft-change-risk
description: "Classify Bitcraft change risk using CSS, DIS, and CIL, and review routing-signal quality (agent/model family used, validation results, rework observations). Use before planning, review, audits, branching decisions, validation routing, and when reviewing whether routing or model choices caused friction."
argument-hint: "[change description, diff, plan, review scope, or routing decision]"
---

# Bitcraft Change Risk

Use this skill to classify impact before implementing, reviewing, or auditing a change, and to reflect on routing quality after the fact.

## Classification Procedure

1. Read `.github/governance/METRICS.md`.
2. Identify the changed behavior, data, users, modules, deployment surfaces, and security/privacy boundaries.
3. Assign Change Severity Score (CSS) from 0-10.
4. Assign Dependency Impact Score (DIS) from 0-10.
5. Derive Change Impact Level (CIL): Low, Medium, High, or Critical.
6. Apply overrides for security, privacy, irreversible data, production deployment, credentials, or Git history risk.
7. Choose the review and validation route from the matching `.github/config/cil-<level>.yaml` protocol.

## Routing Reflection Procedure

1. Read `.github/governance/METRICS.md` and `.github/governance/TOOLING_MATRIX.md`.
2. Determine whether the task needs immediate capture, a retrospective, or a human threshold decision.
3. Record only signals that answer a decision or improve routing: agent, model family, skill entrypoint, CSS/DIS/CIL, validation, review route, and result.
4. Avoid hard gates until the human approves thresholds and capture location.
5. For model comparisons, compare success, rework, cost/tokens, latency, privacy fit, and validation outcome.
6. Create Secretary, Planner, Sec Ops, or AI Ops inbox items when routing decisions need follow-up.

## Output

- CSS and DIS with rationale.
- CIL and overrides.
- Required review route and validation strength.
- Signals captured or intentionally deferred, with interpretation and limits.
- Human decisions or escalations.
