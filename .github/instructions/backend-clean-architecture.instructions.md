---
description: 'Bitcraft backend Clean Architecture and CQRS rules for Domain, Application, Infrastructure, and Api C# code.'
applyTo: 'src/Bitcraft.Domain/**/*.cs,src/Bitcraft.Application/**/*.cs,src/Bitcraft.Infrastructure/**/*.cs,src/Bitcraft.Api/**/*.cs'
---

# Backend Clean Architecture

Apply these rules to backend C# changes.

## Dependency Rules
- Preserve the dependency direction: Api -> Application + Infrastructure, Infrastructure -> Application + Domain, Application -> Domain.
- Do not reference Infrastructure or Api from Domain or Application.
- Do not reference external packages from Application unless they are already permitted dependency abstractions.
- Put abstractions in Application and implementations in Infrastructure.

## CQRS and Data Flow
- Use immutable records for commands, queries, DTOs, responses, and contracts.
- Keep handlers focused on orchestration; push domain invariants into Domain types.
- Never expose Domain entities from API endpoints.
- Application failures should use `Result<T>` rather than exceptions for expected control flow.
- Always accept and pass `CancellationToken` for I/O.

## Domain Modeling
- Keep aggregate-local helper enums, records, and small supporting classes in the aggregate file unless they are reused across aggregates or bounded contexts.
- Tutor is the aggregate root for tutor-scoped model connection, prompt versions, knowledge links, tools, deployment profile, and configuration snapshots.
- Snapshot records are immutable, append-only, and captured with a static `Capture(...)` factory.
- Use `Guid` primary keys and strongly linked `AppUser` foreign keys with EF navigation properties for owner, creator, modifier, and captured-by relationships.

## Implementation Style
- Prefer primary constructors, file-scoped namespaces, early returns, and dependency injection.
- Use `NotImplementedException("Implementation pending for this capability.")` only for an intentional seam that will be implemented later.
- Avoid header bridges, temporary in-memory shims, workaround code paths, and vertical-slice shortcuts in core abstractions.
