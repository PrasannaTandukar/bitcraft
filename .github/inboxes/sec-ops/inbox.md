---
created_at: 2026-05-19
edited_at: 2026-05-21
creator: ai-ops
editor: secretary
ai_gen: true
---
# Sec Ops Inbox

- Date: 2026-05-21
- Status: reviewed (closed)
- Source: Lead Dev (plan `.github/plans/local-setup-revision-2026-05-21/plan.yaml`)
- Target: sec-ops
- Priority: low
- Task: Local-setup revision shipped. Surface notes reviewed in feature-freeze security sweep (2026-05-21): (1) `/api/health` version disclosure — flagged as L-1 (Low); recommendation to gate `version` field behind `IsDevelopment()` and add rate limiting, queued to NET Dev. (2) `.env.example` keys — clean, no secrets. (3) `curl` in runtime image — flagged as L-2 (Low); accepted risk, container isolation adequate. (4) Jetson image — Human-managed; no action.
- Acceptance/Cleanup: Closed. L-1 follow-up queued to NET Dev.

- Date: 2026-05-19
- Status: active
- Source: Planner
- Target: sec-ops
- Priority: high
- Task: After Researcher delivers `RESEARCH_MCP_VALE_SECURITY.md`, run the security audit scoped to: new MCP plan tools (path traversal), CIL protocol YAML files (no executable content), `argument-hint` prompt injection vectors in prompt files, Vale rule injection safety, and updated `mcp-server/server.py` output sanitization. Fix all Critical and High findings immediately. Queue Medium and below as inbox items.
- Context: See `.github/plans/batch-2-agentic-stack/plan.md` WS8. Unblock depends on WS5 research completing and human confirming fetch/git controls (see Open Decisions 4 and 5).
- Acceptance/Cleanup: Audit report produced; critical/high findings fixed or escalated; inbox items created for medium findings; remove this item.

- Date: 2026-05-19
- Status: active
- Source: Human (D-4, D-5)
- Target: sec-ops
- Priority: medium
- Task: Document the activation posture of the `fetch` and `git` MCP servers in `.github/mcp/bitcraft-mcp-spec.md` and `mcp-server/README.md`. `uvx` is installed (verified 2026-05-19); both servers are active. Interim control is per-request approval via the VS Code Configure Tools picker — **no session-wide auto-approval**.
  1. `fetch` (`uvx mcp-server-fetch`): record the per-request approval requirement and prompt-injection warning. A domain allow-list may be added later if research recommends it.
  2. `git` (`uvx mcp-server-git`): enumerate the exact tools exposed by the installed version and document the confirmed-safe read-only tool list (log, diff, show, blame, read_file). Reject any write-capable tools surfaced by the server.
- Context: VS Code MCP sandbox is macOS/Linux only; Windows relies on per-tool approval. Both servers wired in `.vscode/mcp.json`.
- Acceptance/Cleanup: Spec and README updated, confirmed-safe tool list recorded; remove this item.

- Date: 2026-05-18
- Status: parked
- Source: AI Ops
- Target: sec-ops
- Priority: medium
- Task: Run the first Security Audit when the human requests an audit cycle or before a high-impact release.
- Context: Use `.github/audits/templates/audit.template.md`; escalate Critical findings to the human and create remediation inbox items.
- Acceptance/Cleanup: Audit is completed or converted into concrete remediation tasks; remove this item afterward.

- Date: 2026-05-20
- Status: reviewed — M-2 remediation queued to NET Dev
- Source: AI Ops (feature-freeze v3.1 seeding)
- Target: sec-ops
- Priority: high
- Task: Review the centralized ABAC policy evaluator contract (D1) and the `Document.Visibility` first resource attribute (D1.a) before NET Dev locks the interface. Confirm the evaluator never branches in controllers, returns `Result<T>`, and that reserved enum values cannot leak into authorization decisions through default fall-through.
- Context: feature-freeze-2026-05-21 WS1/WS4, D1, D1.a. Pairs with the NET Dev high-priority item of the same date.
- Sweep result (2026-05-21): `PolicyEffect.NotApplicable` false-allow risk confirmed (M-2). `BITCRAFT_RUNTIME_LOCAL_ONLY` scoping is clean (I-1). Admin bootstrap has no weak-default path (I-2). Remediation (`PolicyDecision.IsPermit()` + XML docs) queued to NET Dev.
- Acceptance/Cleanup: Close when NET Dev M-2 item is resolved and `IsPermit()` helper is merged.

- Date: 2026-05-20
- Status: active
- Source: AI Ops (feature-freeze v3.1 seeding)
- Target: sec-ops
- Priority: medium
- Task: Review the `SaveChanges` audit interceptor scope (D10) for PII risk. Confirm the interceptor captures structural metadata only (entity name, key, operation, `CapturedById`), never serialized payloads, and that audit reads honor the same ABAC policy as the underlying aggregate.
- Context: feature-freeze-2026-05-21 WS8, D10.
- Acceptance/Cleanup: Review notes added to the freeze plan reviews log; remove this item.
