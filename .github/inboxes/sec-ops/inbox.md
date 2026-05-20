---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# Sec Ops Inbox

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
