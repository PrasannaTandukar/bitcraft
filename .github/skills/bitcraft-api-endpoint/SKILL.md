---
created_at: 2026-09-21
edited_at: 2026-09-21
creator: ai-ops
editor: ai-ops
ai_gen: true
name: bitcraft-api-endpoint
description: "Implement or change Bitcraft API surface: controllers, contracts, routes, mapping, and Swagger metadata in src/Bitcraft.Api. Use when the work is endpoint-shaped (routes, request/response shapes, authorization attributes) without new Domain or Application behavior. For new CQRS handlers, aggregates, or persistence, use bitcraft-backend-feature instead."
argument-hint: "[endpoint, route, contract, or controller change]"
---

# Bitcraft API Endpoint

Use this skill for endpoint-shaped work in `src/Bitcraft.Api`. Read `.github/instructions/api-contracts.instructions.md` first.

## Procedure

1. Locate the owning controller and contract file under `src/Bitcraft.Api/Contracts/<Context>/`.
2. Keep routes resource-shaped REST. Organize controllers by bounded context.
3. Define request and response as immutable records in the contract file. Never expose Domain entities.
4. Add or update the mapping in `src/Bitcraft.Api/Mapping/`.
5. Set authorization attributes to match the route's data ownership. Verify the policy exists in `Program.cs`.
6. Confirm Swagger metadata: summary, operation id, and response types render correctly.
7. Validate with the narrowest relevant test or build.

## Constraints

- Do not add behavior that belongs in Application handlers. If a new command or query is needed, route to `bitcraft-backend-feature`.
- Do not change frontend files unless explicitly requested.

## Output

- Changed endpoints with route and contract names.
- Validation run.
- Next step: "Run the `bitcraft-review` skill for branch `<branch>`. Scope: <changed files>."
