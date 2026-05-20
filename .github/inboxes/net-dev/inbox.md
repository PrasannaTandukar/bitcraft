---
created_at: 2026-05-19
edited_at: 2026-05-21
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# NET Dev Inbox

- Date: 2026-05-21
- Status: partially complete (skeleton only)
- Source: Lead Dev (plan v3.4 / D24)
- Target: net-dev
- Priority: medium
- Task: ABAC `IPolicyEvaluator` scaffolding for WS1/WS4. Skeleton (interface, `PolicyRequest`, `PolicyDecision`, `PolicyEffect`, `Obligation`) landed in `src/Bitcraft.Application/Common/Authorization/`. Concrete evaluator implementation and the policy catalog still pending — they land in WS2 step 2 / WS6.
- Context: Obligation `Key` vocabulary deliberately left open and will be locked separately (potential D24.a).
- Recently completed (2026-05-21): contracts landed on branch `feature-freeze/01-identity-and-roles-vocabulary`; unit build/tests green.
- Acceptance/Cleanup: Close when the evaluator implementation + policy catalog land and pass tests.

