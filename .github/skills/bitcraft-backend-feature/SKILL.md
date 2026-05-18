---
name: bitcraft-backend-feature
description: 'Implement Bitcraft backend features across Domain, Application, Infrastructure, Api, and tests. Use for CQRS handlers, controllers, contracts, EF persistence, Tutor aggregate work, Documents, Knowledge, Identity, authorization, and validation.'
argument-hint: '[feature, bug, or approved plan]'
---

# Bitcraft Backend Feature

Implement backend changes without breaking Clean Architecture.

## When to Use This Skill

- The task touches `src/Bitcraft.*` C# projects.
- The change needs commands, queries, contracts, controllers, persistence, or tests.
- The user asks for Documents, Knowledge, Tutor, Identity, model catalog, authorization, or auditing behavior.

## Procedure

1. Locate the owning aggregate, handler, controller, contract, interface, or persistence mapping.
2. State the local hypothesis and cheapest validation before editing.
3. Edit from the domain/application boundary outward.
4. Keep Application package-free from Infrastructure concerns.
5. Add or update contracts in `src/Bitcraft.Api/Contracts/<Context>/<Feature>.cs`.
6. Add focused tests for changed Domain/Application behavior when feasible.
7. Validate with the narrowest relevant test or build.

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
