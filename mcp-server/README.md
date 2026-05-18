# Bitcraft MCP Server

Minimal read-only Model Context Protocol server. Exposes Bitcraft governance docs, the agent roster, and per-agent prioritized inboxes to compatible coding agents (GitHub Copilot in VS Code, Claude Desktop, and others).

## Scope

Read-only by design. The server never writes to disk, never runs shell commands, never contacts external services, and rejects any path that leaves the allowed subdirectories.

Tools:

- `list_inboxes()` returns agent slugs that have an inbox.
- `read_inbox(agent_slug)` reads one prioritized backlog.
- `list_governance_docs()` lists docs under `.github/agentic-ai/`.
- `read_governance(name)` reads `PROJECT_BRIEF`, `DEVELOPER_GUIDE`, `METRICS`, `MODEL_ROUTING`, `AUDIT_TEMPLATES`, or `TOOLING_MATRIX`.
- `list_agents()` lists custom agent slugs.
- `read_roster()` reads `.github/agents/ROSTER.md`.

## Install

```powershell
cd mcp-server
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

## Run Locally

```powershell
python server.py
```

The server uses stdio transport and is meant to be launched by a client (VS Code Copilot, Claude Desktop, etc.) rather than run standalone.

## Configure VS Code GitHub Copilot

A workspace configuration is provided at `.vscode/mcp.json`. The `.vscode/` directory is selectively gitignored: `mcp.json`, `settings.json`, `extensions.json`, `tasks.json`, and `launch.json` are tracked so workspace MCP wiring is shareable.

The expected configuration:

```jsonc
{
  "servers": {
    "bitcraft-mcp": {
      "type": "stdio",
      "command": "python",
      "args": ["mcp-server/server.py"]
    }
  }
}
```

Adjust `command` to the absolute path of your virtualenv Python on Windows if VS Code does not pick up the workspace venv.

## Security

- Read-only.
- All file access constrained to `.github/agentic-ai/`, `.github/agents/`, and `.github/inboxes/`.
- Path traversal attempts raise `ValueError`.
- No secrets, credentials, dataset paths, or external endpoints are read or returned.

When extending the server:

1. Keep tools read-only by default.
2. Add allowed directories explicitly; never accept a free-form path argument.
3. Document the new tool in this README and in `.github/mcp/bitcraft-mcp-spec.md`.
4. Request Sec Ops review when adding any tool that surfaces user content, identity data, or model artifacts.
