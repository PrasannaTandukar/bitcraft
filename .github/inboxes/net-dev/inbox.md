---
created_at: 2026-05-19
edited_at: 2026-05-21
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# NET Dev Inbox

- Date: 2026-05-21
- Status: active
- Source: Sec Ops (feature-freeze security sweep 2026-05-21)
- Target: net-dev
- Priority: medium
- Task: Defense-in-depth IDOR guard for TutorRevisions. Add `TutorId` to every command and query record in `TutorRevisionFeatures.cs`. In each handler, assert `revision.TutorId == request.TutorId` and return an opaque `NotFound` if they differ. Currently only the membership check prevents cross-tutor probing; the route `{tutorId}` segment is unused in the command/query path.
- Context: Sec Ops finding M-1. Files: `src/Bitcraft.Api/Controllers/Tutoring/TutorRevisionsController.cs`, `src/Bitcraft.Application/Features/Tutoring/TutorRevisionFeatures.cs`. No live exploit today; membership check saves it. Defense-in-depth ensures any accidental handler omission does not silently expose the gap.
- Acceptance/Cleanup: All command/query records carry `TutorId`; all handlers assert match; unit test covers mismatch case; close this item.

- Date: 2026-05-21
- Status: active
- Source: Sec Ops (feature-freeze security sweep 2026-05-21)
- Target: net-dev
- Priority: medium
- Task: Lock `PolicyEffect.NotApplicable = deny` contract before any caller of `IPolicyEvaluator` is written. (1) Add XML doc to `IPolicyEvaluator.EvaluateAsync` stating `NotApplicable` must be treated as deny. (2) Add XML doc to `PolicyEffect.NotApplicable` with the same statement. (3) Add `bool IsPermit()` helper to `PolicyDecision` returning `Effect == PolicyEffect.Permit` — making the safe check idiomatic and hard to misread.
- Context: Sec Ops finding M-2. File: `src/Bitcraft.Application/Common/Authorization/`. A caller written as `if (decision.Effect != PolicyEffect.Deny)` would silently permit on `NotApplicable`. Must be locked before WS6 concrete evaluator implementation. Also closes the sec-ops 2026-05-20 high item requesting evaluator contract review.
- Acceptance/Cleanup: `PolicyDecision.IsPermit()` added; XML docs on interface and enum present; unit test covers `NotApplicable` treated as deny; sec-ops 2026-05-20 item closed; remove this item.

- Date: 2026-05-21
- Status: active
- Source: Sec Ops (feature-freeze security sweep 2026-05-21)
- Target: net-dev
- Priority: low
- Task: Gate assembly version in the `GET /api/health` response for non-Development deployments. Check `IHostEnvironment.IsDevelopment()` and omit or stub the `version` field in non-dev environments. Also add a fixed-window `AddRateLimiter` policy to the health endpoint (or globally) to prevent saturating requests.
- Context: Sec Ops finding L-1. File: `src/Bitcraft.Api/Controllers/HealthController.cs`, `src/Bitcraft.Api/Program.cs`. Low severity; no DB calls in handler so DoS is limited. Version string helps attackers target specific CVEs in production.
- Acceptance/Cleanup: `version` field gated behind `IsDevelopment()`; rate limiter wired for the `/api/health` route or globally; close this item.

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

