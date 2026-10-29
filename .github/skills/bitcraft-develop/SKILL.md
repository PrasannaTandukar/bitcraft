---
created_at: 2026-09-21
edited_at: 2026-09-21
creator: ai-ops
editor: ai-ops
ai_gen: true
name: bitcraft-develop
description: "Phase 2 entry skill: Development. Executes an approved Bitcraft plan work step by work step with a cheaper or local model. Use when an approved plan exists and the user asks to implement, continue, or finish it."
argument-hint: "[plan path or branch slug]"
---

# Bitcraft Develop (Phase 2 Entry)

Entry skill for the Development phase. Runs inside the Lead Dev agent. A cheaper or local model executes the plan. The plan is the single source of truth. Do not add scope.

## Decision Tree

1. Does an approved plan exist for this branch?
   - No: stop. Route to the `bitcraft-plan` skill first.
   - Yes: read it with `read_plan`. Continue.
2. Are there open human decisions that block a work step?
   - Yes: HALT. List the blocking decisions. Do not guess.
   - No: continue.
3. Which area does the current work step touch? Load exactly one area skill per step.
   - `src/Bitcraft.*` C# projects: `bitcraft-backend-feature`.
   - Controllers, contracts, routes, Swagger only: `bitcraft-api-endpoint`.
   - `tutor-studio/`: `bitcraft-frontend-feature`.
   - `sidecar/` Python: `bitcraft-python-sidecar`.
   - Docker, compose, CI/CD, migrations, release: `bitcraft-devops-release`.
   - `.github` agentic stack: `bitcraft-agentic-stack`.
   - Tests only: `bitcraft-test-hardening`.
   - Dependency or platform version change: `bitcraft-version-upgrade`.

## Procedure

1. Read the plan and its work steps. Confirm the current step number from the plan status.
2. Execute work steps in order. One step at a time.
3. After each step, run the validation command named in the plan. Record the result in the plan status.
4. If a step fails twice, stop. Report the failure with evidence. Do not retry with variations.
5. If reality contradicts the plan (missing file, different signature, new dependency), stop. Report the drift. Route to Phase 3 or back to Phase 1. Do not improvise scope.
6. Keep edits minimal. Reuse existing types, helpers, and patterns before creating new ones.
7. When all steps pass, update the plan status to done and emit the Phase 3 prompt (see Output).

## Constraints

- Do not perform Git write operations. Humans own commits, branches, and pushes.
- Do not touch secrets, credentials, environment files, datasets, or model weights.
- Do not run migrations against shared environments.
- Do not add state, fetches, components, or classes that the plan does not name.

## Output

- Per-step validation results recorded in the plan.
- Drift report if any step contradicted the plan.
- A ready-to-paste Phase 3 prompt: "Run the `bitcraft-review` skill for branch `<branch>`. Scope: the files listed in `.github/plans/<branch-slug>/plan.<slug>.md`. Original request: <goal from plan>."
