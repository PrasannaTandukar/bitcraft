---
created_at: 2026-05-19
edited_at: 2026-05-21
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# NET Dev Inbox

- Date: 2026-05-21
- Status: complete (Iteration 1 — pending PR review)
- Source: Secretary (D33 lock 2026-05-21)
- Target: net-dev
- Priority: complete
- Task: `DocumentResponse` split (D33). Verified by Net Dev: production code already implemented as `DocumentSummaryResponse` (list) + `DocumentDetailResponse` (detail). Added integration test coverage in `tests/Bitcraft.ApiIntegrationTests/DocumentResponseShapeTests.cs`. Build green, 190/190 unit tests pass.
- Acceptance/Cleanup: Close after Iteration 1 PR merges.

 Add `TutorId` to every command and query record in `TutorRevisionFeatures.cs`. In each handler, assert `revision.TutorId == request.TutorId` and return an opaque `NotFound` if they differ. Currently only the membership check prevents cross-tutor probing; the route `{tutorId}` segment is unused in the command/query path.
- Context: Sec Ops finding M-1. Files: `src/Bitcraft.Api/Controllers/Tutoring/TutorRevisionsController.cs`, `src/Bitcraft.Application/Features/Tutoring/TutorRevisionFeatures.cs`. No live exploit today; membership check saves it. Defense-in-depth ensures any accidental handler omission does not silently expose the gap.
- Acceptance/Cleanup: All command/query records carry `TutorId`; all handlers assert match; unit test covers mismatch case; close this item.

- Date: 2026-05-21
- Status: complete (Iteration 1)
- Source: Sec Ops (feature-freeze security sweep 2026-05-21)
- Target: net-dev
- Priority: complete
- Task: `PolicyEffect.NotApplicable` deny-by-default contract. Verified production code already has `PolicyDecision.IsPermit()` plus XML docs on `IPolicyEvaluator.EvaluateAsync` and `PolicyEffect.NotApplicable`. Added test `PolicyDecisionTests.cs` locking the contract (only `Permit` returns true; `Deny` and `NotApplicable` return false).
- Acceptance/Cleanup: Close after Iteration 1 PR merges.

- Date: 2026-05-21
- Status: complete (Iteration 1)
- Source: Sec Ops (feature-freeze security sweep 2026-05-21)
- Target: net-dev
- Priority: complete
- Task: `/api/health` version gate + rate limiter (L-1). Verified production code: `HealthController` injects `IHostEnvironment` and nulls `version` outside Development; `Program.cs` wires global fixed-window `AddRateLimiter` (10 req/sec/IP, queue 0, 429 rejection).
- Acceptance/Cleanup: Close after Iteration 1 PR merges.

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

