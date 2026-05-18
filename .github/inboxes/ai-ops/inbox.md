# AI Ops Inbox

- Date: 2026-05-18
- Status: active
- Source: AI Ops (Agentic Audit F-2)
- Target: ai-ops
- Priority: medium
- Task: Refresh `/memories/repo/*.md` entries to reflect the new layout: `handoffs`→`inboxes`, `AGENTS.md`→`ROSTER.md` (with root `AGENTS.md` added per agents.md), MCP server implemented, `MODEL_ROUTING.md` added.
- Context: Stale repo memory could reintroduce wrong paths in later sessions. Touch only the entries that are factually outdated.
- Acceptance/Cleanup: Repo memory mirrors the current tree; remove this item.

- Date: 2026-05-18
- Status: active
- Source: AI Ops (Agentic Audit F-3)
- Target: ai-ops
- Priority: medium
- Task: After human installs MCP requirements, verify the `bitcraft-mcp` server starts in VS Code and the six read-only tools (`list_inboxes`, `read_inbox`, `list_governance_docs`, `read_governance`, `list_agents`, `read_roster`) respond.
- Context: `python -m pip install -r mcp-server/requirements.txt` then VS Code reload. Wiring lives in `.vscode/mcp.json`.
- Acceptance/Cleanup: Tools respond as expected; remove this item or convert any defects into new inbox items.

- Date: 2026-05-18
- Status: parked
- Source: AI Ops
- Target: ai-ops
- Priority: low
- Task: Schedule the next Agentic Audit after the branch convention and metrics thresholds are approved, or when a material primitive changes (new Copilot/MCP feature, new model family).
- Context: Use `.github/agentic-ai/AUDIT_TEMPLATES.md` and the format established in `AUDIT_2026-05-18_AGENTIC.md`.
- Acceptance/Cleanup: New audit report file is produced and findings converted to inbox items; remove this item afterward.
