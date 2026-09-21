---
created_at: 2026-05-19
edited_at: 2026-09-21
creator: ai-ops
editor: lead-dev
ai_gen: true
name: bitcraft-backend-feature
description: "Implement Bitcraft backend features across Domain, Application, Infrastructure, Api, and tests. Use for CQRS handlers, controllers, contracts, EF persistence, Tutor aggregate work, Documents, Knowledge, Identity, authorization, and validation."
argument-hint: "[feature, bug, or approved plan]"
---

# Bitcraft Backend Feature

Implement backend changes without breaking Clean Architecture.

## When to Use This Skill

- The task touches `src/Bitcraft.*` C# projects.
- The change needs commands, queries, contracts, controllers, persistence, or tests.
- The user asks for Documents, Knowledge, Tutor, Identity, model catalog, authorization, or auditing behavior.

## Procedure

1. Requirement check first: restate the requested behavior in one sentence. Verify every type, fetch, and field you intend to add is named by the request or the plan. If it is not, do not add it.
2. Locate the owning aggregate, handler, controller, contract, interface, or persistence mapping.
3. Reuse before creating: search for an existing type, helper, repository method, or contract that already carries the value. Extend it instead of adding a parallel one.
4. State the local hypothesis and cheapest validation before editing.
5. Edit from the domain/application boundary outward.
6. Keep Application package-free from Infrastructure concerns.
7. Add or update contracts in `src/Bitcraft.Api/Contracts/<Context>/<Feature>.cs`.
8. Add focused tests for changed Domain/Application behavior when feasible.
9. Validate with the narrowest relevant test or build.
10. Emit the next step: "Run the `bitcraft-review` skill for branch `<branch>`. Scope: <changed files>. Original request: <one-sentence restatement from step 1>."

## Clean Architecture Checklist

- Domain/Application do not reference Api or Infrastructure.
- DTOs/contracts are records and do not expose Domain entities.
- Application failures use `Result<T>`.
- I/O accepts and passes `CancellationToken`.
- Ownership, authorization, and privacy checks are preserved.

## Gotchas

- Do not split aggregate-local helper types into separate files unless they are reused.
- Do not add in-memory shims or header bridges.
- Do not change frontend files unless explicitly requested.
