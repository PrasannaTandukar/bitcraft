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
- Task: ABAC `IPolicyEvaluator` scaffolding for WS1/WS4. Skeleton (interface, `PolicyRequest`, `PolicyDecision`, `PolicyEffect`, `Obligation`) landed in `src/Bitcraft.Application/Common/Authorization/`. Concrete evaluator implementation and the policy catalog still pending — they land in WS6.
- Context: Obligation `Key` vocabulary now locked as **D24.a** (audit-log, redact-pii, scope-down enforced; require-step-up reserved). See `plan.decisions.md` §D24.a.
- Recently completed (2026-05-21): contracts landed on branch `feature-freeze/01-identity-and-roles-vocabulary`; unit build/tests green.
- Acceptance/Cleanup: Close when the evaluator implementation + policy catalog land and pass tests.

- Date: 2026-05-21
- Status: complete (read path); follow-up open (grant/revoke endpoints)
- Source: Lead Dev (plan v3.4 / D25 step 2)
- Target: net-dev
- Priority: medium
- Task: WS2 step 2 — Resource-Membership-Tables. `TutorMember` + `ResourceRole` + `ITutorMembershipReader` + handler authorization replacement landed on `feature-freeze/02-resource-membership-tables` (4 commits). Build 0/0; 136/136 unit tests green.
- Follow-up: design + implement `GrantTutorMembership` / `RevokeTutorMembership` / `ChangeTutorMemberRole` endpoints (Owner-only; enforce `WouldLeaveNoOwner`). Out of scope for step 2 PR; required before WS3 step 3 UI work mutates membership.
- Acceptance/Cleanup: Close the follow-up when grant/revoke handlers + controllers + tests land.

