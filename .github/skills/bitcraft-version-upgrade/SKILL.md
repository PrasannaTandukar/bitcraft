---
created_at: 2026-05-19
edited_at: 2026-09-21
creator: ai-ops
editor: lead-dev
ai_gen: true
name: bitcraft-version-upgrade
description: "Plan and execute Bitcraft dependency, framework, model, tool, and platform upgrades. Use for .NET, EF Core, PostgreSQL/pgvector, React/Vite, Copilot customization syntax, Ollama models, Docker, and CI/CD version changes."
argument-hint: "[package, framework, model, or platform to upgrade]"
---

# Bitcraft Version Upgrade

Use this skill for controlled upgrades without dependency drift.

## When to Use This Skill

- A package, framework, AI model, Docker image, or toolchain needs updating.
- VS Code/Copilot customization syntax may have changed.
- A new model or local inference path needs compatibility checks.

## Procedure

1. Inventory current versions from project files, lockfiles, Docker files, and settings.
2. Check official release notes or docs for breaking changes.
3. Identify affected code, tests, configuration, migrations, and docs.
4. Prefer one upgrade axis per iteration unless versions are coupled.
5. Validate with focused build/test/lint commands.
6. Document operational changes and required developer actions.

## Risk Checks

- Database migration compatibility.
- EF provider behavior changes.
- JWT/Identity security defaults.
- Vite/React build and ESLint changes.
- Copilot frontmatter and agent/skill loadability.
- Ollama model memory footprint on 2x RTX 3090.

## Gotchas

- Do not update generated lockfiles or package versions outside the authorized scope.
- Do not assume cloud model pricing or availability is stable.

## Output

- Versions changed, with old and new values.
- Validation run.
- Required developer actions.
- Next step: "Run the `bitcraft-review` skill for branch `<branch>`. Scope: <changed files>."
