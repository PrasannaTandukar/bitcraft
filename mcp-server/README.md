# Bitcraft MCP Server

Minimal read-only Model Context Protocol server. Exposes Bitcraft governance docs, the agent roster, and per-agent prioritized inboxes to compatible coding agents (GitHub Copilot in VS Code, Claude Desktop, and others).

## Scope

Read-only by design. The server never writes to disk, never runs shell commands, never contacts external services, and rejects any path that leaves the allowed subdirectories.

Tools:

- `list_inboxes()` returns agent slugs that have an inbox.
- `read_inbox(agent_slug)` reads one prioritized backlog.
- `list_governance_docs()` lists docs under `.github/agentic-ai/`.
- `read_governance(name)` reads `PROJECT_BRIEF`, `DEVELOPER_GUIDE`, `METRICS`, `MODEL_ROUTING`, `AUDIT_TEMPLATES`, `TOOLING_MATRIX`, `PROVENANCE`, or `CONTENT_EXCLUSION`.
- `list_agents()` lists custom agent slugs.
- `read_roster()` reads `.github/agents/ROSTER.md`.
- `verify_architecture_readonly()` reports Clean Architecture rule violations in `Bitcraft.Domain` and `Bitcraft.Application` (forbidden project references, forbidden `using` statements, forbidden external package references in Application). Read-only spot check only; tests and CI remain the enforcement layer.

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
- All file access constrained to `.github/agentic-ai/`, `.github/agents/`, `.github/inboxes/`, and (for `verify_architecture_readonly` only) `src/Bitcraft.Domain/` and `src/Bitcraft.Application/`.
- Path traversal attempts raise `ValueError`.
- No secrets, credentials, dataset paths, or external endpoints are read or returned.

When extending the server:

1. Keep tools read-only by default.
2. Add allowed directories explicitly; never accept a free-form path argument.
3. Document the new tool in this README and in `.github/mcp/bitcraft-mcp-spec.md`.
4. Request Sec Ops review when adding any tool that surfaces user content, identity data, or model artifacts.

## Additional MCP Servers Wired In `.vscode/mcp.json`

`.vscode/mcp.json` also references two reference servers from the modelcontextprotocol/servers project. Install [`uv`](https://docs.astral.sh/uv/) first (PowerShell: `winget install --id=astral-sh.uv -e`), then VS Code will launch them on demand via `uvx`:

- `fetch` (`uvx mcp-server-fetch`) — web-page fetching for research.
- `git` (`uvx mcp-server-git --repository ${workspaceFolder}`) — read-only Git history, log, diff, blame.

Both run on stdio with no project-level state. **Sec Ops sign-off is pending** — see the known security issues below and `.github/mcp/bitcraft-mcp-spec.md`.

## Known Security Issues (Open)

### Issue 1 — `fetch` MCP: Prompt Injection via Returned Web Content

**Severity:** Medium  
**Status:** Open — Sec Ops sign-off pending.

The `fetch` server fetches any URL and returns its full content into agent context. A web page with embedded adversarial text can instruct the agent to perform unintended actions (prompt injection).

**Workaround:** In VS Code, open **Settings → Configure Tools** (or the MCP tools picker) and set `fetch` to **require per-request approval** rather than auto-approve. Never pre-approve `fetch` for an entire session. Treat all content returned by `fetch` as untrusted user data.

**Risk if not followed:** An agent doing research may retrieve a malicious page and execute embedded instructions — for example, leaking file contents, generating unwanted code, or issuing unexpected follow-up tool calls. This risk is heightened in sessions where the agent has broad file-read access.

### Issue 2 — `git` MCP: Write-Capable Tools May Be Exposed

**Severity:** Medium  
**Status:** Open — Sec Ops tool inventory check pending.

`mcp-server-git` may expose write-capable tools (`git_create_branch`, `git_checkout`, `git_commit`, `git_push`) depending on the version installed by `uvx`. The spec intends read-only use only.

**Workaround:** In VS Code, open **Settings → Configure Tools** and approve only the following read-only tools: `git_log`, `git_diff`, `git_show`, `git_blame`, `git_read_file`. Decline or disable any other `git_*` tools presented. Do not grant blanket session approval to the `git` server.

**Risk if not followed:** An agent with broad `git` tool access may commit, branch, or push on behalf of the user — bypassing the project's rule that humans own all Git write operations. This could result in unreviewed commits or accidental history modification.

---

Once Sec Ops completes the sign-off (`.github/inboxes/sec-ops/inbox.md`), this section will be updated with the confirmed-safe tool list and any approved allow-list configuration.
