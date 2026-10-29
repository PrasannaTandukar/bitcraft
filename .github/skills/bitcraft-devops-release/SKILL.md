---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
name: bitcraft-devops-release
description: 'Plan Bitcraft deployment, CI/CD, Docker, environment, migration, release operations, branch conventions, and Integrity Audits. Use for build/test pipelines, compose files, database updates, Bitbucket/Jira workflow, rollout checks, and production readiness.'
argument-hint: '[deployment, CI/CD, migration, branch policy, integrity audit, or release concern]'
---

# Bitcraft DevOps Release

Use this skill for operational readiness and deployment workflows.

## When to Use This Skill

- The user asks about CI/CD, Docker, deployments, migrations, build tasks, branch conventions, integrity audits, or release validation.
- A backend change affects environment variables, appsettings, database migrations, or external services.
- The agentic AI setup needs validation or rollout guidance.

## Procedure

1. Inventory affected services and configuration.
2. Identify secrets, environment variables, ports, volumes, and database dependencies.
3. Keep generated operational changes scoped to the authorized files.
4. Plan migrations separately from code deployment when risk warrants it.
5. Validate with the narrowest build/test/container command available.
6. For branch policy, keep Git write operations human-owned and evaluate short-lived Bitbucket/Jira-compatible branches.
7. For Integrity Audits, use `.github/audits/templates/audit.template.md` and create inbox items for remediation.
8. Document rollback and manual operator steps.

## Checks

- `dotnet build` for backend compile changes.
- Focused `dotnet test` for behavior changes.
- Docker Compose changes validated by config/render checks when available.
- EF migration changes reviewed for data loss.
- Copilot customization changes validated by diagnostics/frontmatter scans.

## Gotchas

- Do not run database updates against production by default.
- Do not add CI/CD secrets or credentials to repository files.
- Do not perform Git writes.
- Do not approve branch conventions or release gates without human confirmation.
