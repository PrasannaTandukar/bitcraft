# Sec Ops Inbox

- Date: 2026-05-19
- Status: blocked
- Blocked by: Human must confirm the accepted interim control for `fetch` (per-request vs allow-list), and a human or agent must run `uvx mcp-server-git` and enumerate exposed tools for the `git` server.
- Source: Post-dev review (Secretary, 2026-05-19)
- Target: sec-ops
- Priority: medium
- Task: Sign off on the two third-party MCP servers in `.vscode/mcp.json` and document the approved mitigations in `bitcraft-mcp-spec.md`.
  1. `fetch` (`uvx mcp-server-fetch`): prompt-injection risk on returned web content. Confirm "per-request human approval via VS Code Configure Tools" is the accepted interim control, or define a domain allow-list. No auto-approval for all sessions.
  2. `git` (`uvx mcp-server-git`): enumerate the exact tools exposed by the installed version of `mcp-server-git`. Confirm only read-only tools (log, diff, show, blame, read_file) are approved. Document the confirmed-safe tool list.
- Context: VS Code MCP sandbox is macOS/Linux only; Windows relies on per-tool approval. Both servers are wired but neither is auto-approved. Security issues and required workarounds are documented in `mcp-server/README.md` and `bitcraft-mcp-spec.md`.
- Acceptance/Cleanup: Mitigations documented in `bitcraft-mcp-spec.md`, approved tool list recorded, Known Open Security Findings section updated; remove this item.

- Date: 2026-05-18
- Status: parked
- Source: AI Ops
- Target: sec-ops
- Priority: medium
- Task: Run the first Security Audit when the human requests an audit cycle or before a high-impact release.
- Context: Use `.github/agentic-ai/AUDIT_TEMPLATES.md`; escalate Critical findings to the human and create remediation inbox items.
- Acceptance/Cleanup: Audit is completed or converted into concrete remediation tasks; remove this item afterward.
