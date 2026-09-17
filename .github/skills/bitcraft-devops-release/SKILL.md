---
created_at: 2026-05-19
edited_at: 2026-09-17
creator: ai-ops
editor: ai-ops
ai_gen: true
name: bitcraft-devops-release
description: "Plan and review Bitcraft deployment, CI/CD, Docker, environment, EF migration, release operations, and branch conventions. Use for build/test pipelines, compose files, database updates, Bitbucket/Jira workflow, rollout checks, production readiness, and Integrity Audits."
argument-hint: "[pipeline, Docker, migration, deployment, branch policy, release, or integrity audit]"
---

# Bitcraft DevOps Release

Use this skill for infrastructure, release, and operational integrity work. Runs inside the Sec Ops agent (the single integrity owner).

## Procedure

1. Read `.github/governance/METRICS.md` and classify the change with the `bitcraft-change-risk` skill.
2. Scope the surface: build, tests, Docker, compose, CI/CD, migrations, environments, release, rollback, branch workflow.
3. Verify current state in repository files before proposing changes — do not plan from stale memory.
4. Keep changes minimal and validate with the narrowest relevant command (`docker compose config --quiet`, build, focused tests).
5. Escalate high-risk deployment, migration, rollback, branch, or environment issues to the human.

## Migration Rules

- Agents never run migrations against shared environments. Humans apply them.
- Document the command, the prerequisite migrations, the post-apply verification query, and the rollback risk for every migration item.
- Note lossy `Down()` paths explicitly.

## Integrity Audit Procedure

1. Read `.github/audits/templates/audit.template.md` and the Sec Ops inbox.
2. Scope build, tests, Docker, compose, CI/CD, migrations, environments, release, rollback, and branch workflow.
3. Produce findings with severity, CSS, DIS, CIL, evidence, owner, and remediation.
4. Create inbox items for remediation owners.

## Constraints

- Do not perform Git write operations.
- Do not create active MCP, CI, credential, or deployment configs containing secrets.
- Do not run destructive database or deployment commands without explicit human approval.

## Output

- Operational finding or change.
- Commands validated or intentionally not run.
- Branch/release implications.
- CIL and review route.
- Inbox updates.
