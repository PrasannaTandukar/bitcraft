---
created_at: 2026-09-21
edited_at: 2026-09-21
creator: ai-ops
editor: ai-ops
ai_gen: true
name: bitcraft-plan
description: "Phase 1 entry skill: Planning and Research. Converts a goal into a detailed, low-ambiguity technical plan that a cheaper or local model can execute without interpretation. Use when a branch is ready for agentic work and no approved plan exists, or when the user asks to plan."
argument-hint: "[goal, feature, bug, or research question]"
---

# Bitcraft Plan (Phase 1 Entry)

Entry skill for the Planning and Research phase. Runs inside the Planner agent. A capable cloud model produces the plan. The plan must be detailed enough for a weaker local model to execute step by step.

## Decision Tree

1. Is the goal already covered by an active plan under `.github/plans/<branch-slug>/`?
   - Yes: read it with `read_plan`. Update it instead of creating a new one.
   - No: continue.
2. Does the task need evidence before planning (versions, external behavior, architecture choice)?
   - Yes: run the `bitcraft-research` skill first. Feed its findings into the plan.
   - No: skip research.
3. Which area does the change touch? Pick every matching area. The plan names the Phase 2 area skill per work step.
   - `src/Bitcraft.*` C# projects: `bitcraft-backend-feature`.
   - Controllers, contracts, routes, Swagger only: `bitcraft-api-endpoint`.
   - `tutor-studio/`: `bitcraft-frontend-feature`.
   - `sidecar/` Python: `bitcraft-python-sidecar`.
   - Docker, compose, CI/CD, migrations, release: `bitcraft-devops-release`.
   - `.github` agentic stack: `bitcraft-agentic-stack`.
   - Tests only: `bitcraft-test-hardening`.
   - Dependency or platform version change: `bitcraft-version-upgrade`.
4. Is the prose of the plan agent-facing (acceptance criteria, work steps)?
   - Yes: apply the `bitcraft-ste` skill in Strict mode.

## Procedure

1. Read `.github/governance/DEVELOPER_GUIDE.md`, `.github/governance/METRICS.md`, and the Planner inbox.
2. Verify current state from repository files. Do not plan from memory. Use `read_plan`, `read_governance`, and `read_config` MCP tools for scoped reads.
3. Classify CSS, DIS, and CIL with the `bitcraft-change-risk` skill.
4. Write the plan under `.github/plans/<branch-slug>/` per `.github/plans/README.md`. Derive the branch slug from the working Git branch (lowercase, `/` to `-`). HALT on `dev`, `main`, or `test`.
5. Structure each work step for a weaker model:
   - Name the exact files and symbols to touch.
   - State the change in one imperative sentence.
   - State the validation command and the expected result.
   - Name the Phase 2 area skill that executes the step.
6. Include goal, non-goals, assumptions, risks, acceptance criteria, open human decisions, and the delegation route.
7. End the plan with a ready-to-paste Phase 2 prompt (see Output).

## Constraints

- Do not implement production code during planning.
- Do not decide branch policy, metrics thresholds, or release policy without human input.
- Do not leave a work step that requires interpretation. If a step needs a decision, record it as an open human decision and HALT the plan on it.

## Output

- The persisted plan files.
- CSS, DIS, CIL, and the review route.
- Open human decisions, if any.
- A ready-to-paste Phase 2 prompt: "Run the `bitcraft-develop` skill for `.github/plans/<branch-slug>/plan.<slug>.md`. Execute work steps in order. Validate each step before the next."
