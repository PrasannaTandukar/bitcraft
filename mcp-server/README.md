---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: human
editor: lead-dev
ai_gen: false
---
# Bitcraft MCP Server

Minimal read-only Model Context Protocol server. Exposes Bitcraft governance docs, the agent roster, and per-agent prioritized inboxes to compatible coding agents (GitHub Copilot in VS Code, Claude Desktop, and others).

## Scope

Read-only by design. The server never writes to disk, never runs shell commands, never contacts external services, and rejects any path that leaves the allowed subdirectories.

Tools:

- `list_inboxes()` returns agent slugs that have an inbox.
- `read_inbox(agent_slug)` reads one prioritized backlog.
- `read_inbox_structured(agent_slug)` reads one prioritized backlog as parsed structured items while preserving the Markdown source of truth.
- `list_governance_docs()` lists docs under `.github/governance/`.
- `read_governance(name)` reads stable governance docs such as `PROJECT_BRIEF`, `DEVELOPER_GUIDE`, `METRICS`, `MODEL_ROUTING`, `TOOLING_MATRIX`, `PROVENANCE`, `CONTENT_EXCLUSION`, `PROMPT_STACK_MAINTENANCE`, `BRANCH_CONVENTION`, or `HUMAN_DECISIONS`. (Audits, plans, research, and reviews live in their own top-level folders.)
- `list_configs()` lists human-managed YAML configuration files under `.github/config/`.
- `read_config(name)` reads one config file (e.g. `approved-external-sources.yaml`, `cil-medium.yaml`). Only `.yaml`/`.yml` files are exposed.
- `list_active_plans(include_archived=False)` lists active plan branch folders under `.github/plans/`, excluding `archive/` and `templates/`. Pass `include_archived=True` to inspect history.
- `read_plan(branch_slug)` reads `plan.yaml`, optional `plan.md`, and optional `context.md` for one active plan folder.
- `list_doc_stacks()` lists developer-local documentation stacks under ignored `docs/dev/`, returning an empty list when absent.
- `read_doc_stack(stack_slug)` reads one `docs/dev/<stack-slug>/STACK.md` manifest and reports local source paths.
- `list_agents()` lists custom agent slugs.
- `read_roster()` reads `.github/agents/ROSTER.md`.
- `list_skills()` lists skills under `.github/skills/` with slug, name, and description. Entry skills (`bitcraft-plan`, `bitcraft-develop`, `bitcraft-review`) are the Phase 1/2/3 workflow entrypoints.
- `read_skill(slug)` reads one skill `SKILL.md` by folder slug.
- `verify_architecture_readonly()` reports Clean Architecture rule violations in `Bitcraft.Domain` and `Bitcraft.Application` (forbidden project references, forbidden `using` statements, and external package references in Application that are not in the in-server allow-list). Read-only spot check only; tests and CI remain the enforcement layer.

## Developer-Local Documentation Stacks

`docs/dev/` is intentionally ignored by Git. Humans and developers can populate it locally with documentation stacks that agents consult before external retrieval.

Expected shape:

```text
docs/dev/<stack-slug>/
  STACK.md
  sources/
    <human-provided files>
```

`STACK.md` should include the repository Markdown metadata fields plus stack fields such as `name`, `description`, `sources`, `external_sources`, and `validation_status`. The MCP server tolerates `docs/dev/` being absent or empty.

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
- All file access constrained to `.github/governance/`, `.github/agents/`, `.github/skills/`, `.github/inboxes/`, ignored `docs/dev/` stack manifests, and (for `verify_architecture_readonly` only) `src/Bitcraft.Domain/` and `src/Bitcraft.Application/`.
- Path traversal attempts raise `ValueError`.
- No secrets, credentials, dataset paths, or external endpoints are read or returned.

When extending the server:

1. Keep tools read-only by default.
2. Add allowed directories explicitly; never accept a free-form path argument.
3. Document the new tool in this README and in `.github/mcp/bitcraft-mcp-spec.md`.
4. Request Sec Ops review when adding any tool that surfaces user content, identity data, or model artifacts.

## Additional MCP Servers Wired In `.vscode/mcp.json`

`.vscode/mcp.json` also references two reference servers from the modelcontextprotocol/servers project. Prerequisite: [`uv`](https://docs.astral.sh/uv/) must be installed (PowerShell: `winget install --id=astral-sh.uv -e`). VS Code launches them on demand via `uvx`.

- `fetch` (`uvx mcp-server-fetch`) — web-page fetching for research.
- `git` (`uvx mcp-server-git --repository ${workspaceFolder}`) — read-only Git history, log, diff, blame.

Both run on stdio with no project-level state. Both are **active under per-request approval** via the VS Code Configure Tools picker (no session-wide auto-approval). See `.github/governance/HUMAN_DECISIONS.md` D-4/D-5 and the security notes below.

## Operating Notes For `fetch` And `git`

### `fetch` MCP: Prompt Injection Awareness

The `fetch` server fetches any URL and returns its full content into agent context. A web page with embedded adversarial text can attempt to instruct the agent to perform unintended actions.

**Required operating practice:** Keep `fetch` set to **require per-request approval** in VS Code's Configure Tools picker. Never enable session-wide auto-approval. Treat all content returned by `fetch` as untrusted user data. A domain allow-list may be introduced later if research recommends it.

### `git` MCP: Read-Only Tool Discipline

`mcp-server-git` may expose write-capable tools (`git_create_branch`, `git_checkout`, `git_commit`, `git_push`) depending on the version installed by `uvx`. The project policy is read-only Git use by agents.

**Required operating practice:** In VS Code, approve only read-only `git` tools (`git_log`, `git_diff`, `git_show`, `git_blame`, `git_read_file`). Decline or disable any other `git_*` tools presented. Do not grant blanket session approval to the `git` server. Sec Ops will record the confirmed-safe tool list in `.github/mcp/bitcraft-mcp-spec.md` after enumerating the installed server version.
