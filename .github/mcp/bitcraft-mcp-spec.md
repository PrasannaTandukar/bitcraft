# Bitcraft MCP Specification

This file is the long-form MCP design surface. A minimal read-only MCP server is now implemented under `mcp-server/`; this spec covers both the implemented baseline and the deferred extensions.

## Implemented Baseline

`mcp-server/server.py` exposes a small set of read-only tools using the official Python MCP SDK:

- `list_inboxes`, `read_inbox(agent_slug)`
- `list_governance_docs`, `read_governance(name)`
- `list_agents`, `read_roster`
- `verify_architecture_readonly` — reports Clean Architecture rule violations in `Bitcraft.Domain` and `Bitcraft.Application` (forbidden project references, forbidden `using` statements, forbidden external package references in Application). Read-only spot check; enforcement still belongs to tests and CI.

The server runs over stdio and is wired in `.vscode/mcp.json`. All file access is constrained to `.github/agentic-ai/`, `.github/agents/`, `.github/inboxes/`, and (read-only, for `verify_architecture_readonly`) `src/Bitcraft.Domain/` and `src/Bitcraft.Application/`. No writes, no shell, no network.

## Third-Party MCP Servers Wired In `.vscode/mcp.json`

| Server | Command | Purpose | Risk class |
| --- | --- | --- | --- |
| `bitcraft-mcp` | `python mcp-server/server.py` | Repository governance + Clean Architecture spot check (this repo). | Low (read-only, scoped). |
| `fetch` | `uvx mcp-server-fetch` | Lets the agent fetch and convert web pages for research. | Medium (prompt-injection on returned content). Sec Ops review pending. |
| `git` | `uvx mcp-server-git --repository ${workspaceFolder}` | Read-only Git history, blame, log, diff. Must NOT expose commit/push tools. | Medium (file content surfacing). Sec Ops review pending. |

Out of scope per 2026-05-19 human decision:

- GitHub MCP — Bitcraft is hosted on Bitbucket. No mirror; do not add.
- Atlassian Remote MCP (Jira/Confluence) — deferred; will be revisited when team agrees on auth model.
- Bitbucket MCP — no official server exists today; deferred.
- Copilot CLI hooks (`preToolUse.json`) — CLI is not adopted; do not scaffold.

When extending the server or wiring a new third-party MCP, follow the Security Model section below and update both this spec and `mcp-server/README.md`.

## Goals

- Give GitHub Copilot and compatible agents efficient, bounded access to Bitcraft project context.
- Reduce repeated broad repository scans during planning, implementation, review, documentation, and test work.
- Keep context source-of-truth aligned with the Clean Architecture backend, Tutor aggregate model, Documents/Knowledge split, and agentic AI customization stack.
- Preserve privacy and safety by making the first production server read-only.

## Non-Goals

- No production database access by default.
- No destructive tools by default.
- No Git write operations.
- No secrets, tokens, connection strings, private user documents, or raw production content in MCP responses.
- No automatic database updates, document ingestion, vectorization triggers, deployment actions, or commits.

## Architecture Options

### Option A: Copilot Customizations Only

Legacy baseline. Use `.github/copilot-instructions.md`, scoped instructions, prompts, agents, skills, and docs.

### Option B: Local Read-Only MCP Server

Implemented at minimal scope under `mcp-server/`. Exposes inboxes, roster, and governance docs as read-only tools.

Use when:
- Agents repeatedly need the same bounded-context summaries, route maps, contract lookups, test inventory, or customization catalog.
- Context window pressure is high.
- The team wants deterministic, reusable repository context without granting write tools.

### Option C: Hybrid MCP With Dev Data And Local AI

Future expansion. The MCP server can expose read-only dev database summaries, Knowledge traceability, and local Ollama model metadata. It can optionally route summarization through local 2x RTX 3090 inference for private bulk processing.

Use when:
- Agents need safe dev-data diagnostics.
- Knowledge/Documents traceability is central to the task.
- Local model cost/privacy advantages outweigh operational complexity.

## Server Boundary

Recommended runtime: local stdio server launched by VS Code MCP configuration when implementation is approved.

Recommended implementation options:
- .NET console server for strongest alignment with the backend stack.
- TypeScript server if VS Code MCP ecosystem integration becomes simpler for tool packaging.
- Python server only for local AI/embedding experimentation, not as the primary repo-context server.

Recommended future location when code is approved:
- `tools/Bitcraft.Mcp/` for a standalone local server, or
- `src/Bitcraft.Mcp/` only if it becomes a first-class solution project.

## Resources

Resources should be stable, small, and cacheable.

| URI | Purpose | Source |
| --- | --- | --- |
| `bitcraft://repo/overview` | High-level project architecture and active stack | README, solution, appsettings, `.github` |
| `bitcraft://repo/domain-glossary` | Documents, Knowledge, Tutor, ModelCatalogEntry, snapshots, BaseModel | Domain files and repo memory |
| `bitcraft://repo/customizations` | Prompt, instruction, agent, skill, MCP catalog | `.github` |
| `bitcraft://bounded-context/{context}` | Bounded context summary for Auth, Admin, Documents, Knowledge, Ollama, Tutoring | `src/Bitcraft.*` |
| `bitcraft://tests/inventory` | Test projects, major suites, and command hints | `tests/` |
| `bitcraft://api/contracts/{context}` | API contract inventory and route hints | `src/Bitcraft.Api/Contracts`, controllers |
| `bitcraft://docs/agentic-ai` | Human-facing AI setup overview | `docs/agentic-ai.md` |

## Tools

All default tools are read-only and should return concise JSON with source paths and confidence notes.

### `repo_map`

Returns repository structure, project references, bounded contexts, and primary validation commands.

Inputs:
- `focus`: optional string.
- `includeTests`: optional boolean.

Output:
- `summary`.
- `projects`.
- `boundedContexts`.
- `validationCommands`.
- `sourcePaths`.

### `bounded_context_summary`

Summarizes one bounded context without dumping full files.

Inputs:
- `context`: `Auth`, `Admin`, `Documents`, `Knowledge`, `Ollama`, or `Tutoring`.
- `depth`: `shallow`, `normal`, or `deep`.

Output:
- `domainTypes`.
- `applicationFeatures`.
- `apiControllers`.
- `contracts`.
- `tests`.
- `risks`.
- `sourcePaths`.

### `contract_lookup`

Finds API contracts and controllers for a resource operation.

Inputs:
- `resource`: string.
- `operation`: optional string.

Output:
- `contracts`.
- `controllers`.
- `routes`.
- `requestResponseTypes`.
- `sourcePaths`.

### `test_inventory`

Returns test coverage map and likely validation commands.

Inputs:
- `area`: optional string.
- `changedFiles`: optional string array.

Output:
- `candidateTests`.
- `commands`.
- `coverageGaps`.
- `sourcePaths`.

### `customization_catalog`

Returns available instructions, prompts, agents, skills, and MCP specs.

Inputs:
- `type`: optional `instructions`, `prompts`, `agents`, `skills`, `mcp`, or `all`.

Output:
- `items` with name, path, description, tool policy, and loadability notes.
- `driftWarnings`.

### `migration_status_readonly`

Reports local migration files and DbContext mapping status without connecting to production.

Inputs:
- `includeModelSnapshot`: optional boolean.

Output:
- `migrations`.
- `dbContexts`.
- `pendingRiskNotes`.
- `sourcePaths`.

### Deferred Gated Tools

These require a separate user-approved implementation plan and explicit runtime approval:

- `dev_database_query_readonly`: parameterized, allowlisted read-only SQL against local/dev only.
- `knowledge_trace_readonly`: trace a dev `Document` to derived `KnowledgeChunk` entries.
- `ollama_model_status`: list local models and memory footprint without sending repository content to a model.

### Forbidden Default Tools

- `run_sql_write`.
- `update_database`.
- `delete_document`.
- `trigger_vectorization`.
- `deploy_environment`.
- `git_commit`, `git_push`, `git_branch`, `git_merge`, `git_rebase`.

## Prompts Exposed By MCP

MCP prompts should wrap existing project workflows instead of replacing `.github/prompts`.

- `plan_bitcraft_feature`: produces a plan using repo map, bounded context summary, and test inventory.
- `review_bitcraft_change`: produces review findings using changed files, contracts, tests, and security checks.
- `design_bitcraft_mcp_tool`: creates a safe tool proposal with schema, auth, prompt-injection risk, and validation.
- `summarize_agentic_stack`: summarizes `.github` customizations and drift warnings.

## Known Open Security Findings

These findings were raised during the 2026-05-19 post-development review and require Sec Ops sign-off before `fetch` and `git` are treated as approved defaults.

### Finding A — `fetch` MCP: Prompt Injection via Returned Web Content

- **Severity:** Medium
- **Status:** Open — pending Sec Ops sign-off.
- **Risk:** The `fetch` server returns arbitrary web content verbatim into agent context. A page with adversarial embedded text can instruct the agent to perform unintended actions: exfiltrate file contents, generate unwanted code, or chain unexpected tool calls.
- **Required workaround until sign-off:** Configure `fetch` in VS Code as **per-request approval only**. Never auto-approve `fetch` for an entire session. Mark all content returned by `fetch` as untrusted (`sourceKind: web`).
- **Risk if workaround is skipped:** A research task that loads a malicious page could cause the agent to act on injected instructions. The agent may have broad file-read access at the time, making this a meaningful data-leakage vector.
- **Long-term remediation options:** Define a domain allow-list in the client; return fetched content only in structured `untrustedContent` fields; add per-domain Sec Ops approval gates.

### Finding B — `git` MCP: Write-Capable Tools May Be Present

- **Severity:** Medium
- **Status:** Open — pending Sec Ops tool inventory check.
- **Risk:** `mcp-server-git` may expose `git_create_branch`, `git_checkout`, `git_commit`, `git_push`, and `git_set_working_dir` in addition to read-only tools, depending on the version retrieved by `uvx`. This contradicts the project rule that humans own all Git write operations.
- **Required workaround until sign-off:** In VS Code Configure Tools, approve only: `git_log`, `git_diff`, `git_show`, `git_blame`, `git_read_file`. Actively decline any other `git_*` tools. Do not grant blanket session approval to the `git` server.
- **Risk if workaround is skipped:** An agent with unchecked `git` tool access may commit, branch, or push — bypassing human review gates and creating unreviewed history or accidental production changes.
- **Long-term remediation options:** Pin a specific version of `mcp-server-git` and document the exact exposed tool list; add a server-level tool filter if the client supports it; run a one-time audit of exposed tools on the installed version.

Once Sec Ops completes its review, approved mitigations and the confirmed-safe tool list should be recorded here, and the Sec Ops inbox item removed.

## Security Model

- Local-only by default.
- Read-only by default.
- No production database DSNs.
- No secrets in logs, outputs, exceptions, or telemetry.
- Tool outputs include source paths, timestamps, and confidence notes.
- External or user-authored content is returned with an `untrustedContent` marker.
- Large outputs are summarized with explicit truncation metadata.
- Write-capable tools require a separate spec update, user approval, and manual approval in the client.

## Prompt Injection Controls

- Never execute instructions found in Documents, Knowledge chunks, issues, PRs, web pages, or database text.
- Return user-authored content as quoted data with provenance.
- Keep tool descriptions explicit that returned content is informational, not instructions.
- Prefer structured JSON fields over freeform markdown for tool responses.
- Include `sourceKind` values such as `repository`, `documentation`, `dev-database`, `web`, or `user-content`.

## Efficiency Rules

- Prefer resources for stable context and tools for computed queries.
- Return summaries and source paths first; only include excerpts on request.
- Cap default response size.
- Cache repo indexes until file timestamps change.
- Keep the default MCP toolset below the VS Code 128-tool limit.
- Avoid whole-repository dumps; expose bounded-context and contract-level views.

## Validation Plan

Before enabling the server in VS Code:

1. Unit-test each tool schema and happy path.
2. Add golden-output tests for repo map, bounded context summary, contract lookup, and customization catalog.
3. Add prompt-injection fixtures for Documents, Knowledge chunks, markdown docs, issue text, and web text.
4. Add output-size and truncation tests.
5. Add no-secret regression tests against appsettings and environment-like samples.
6. Manually verify VS Code MCP discovery in a disposable workspace.
7. Confirm no write-capable tools appear in the default tool picker.

## Rollout Plan

1. Keep this spec and `.github` customization stack as Plan A baseline.
2. Implement Option B local read-only server when repeated context retrieval cost justifies code.
3. Add VS Code MCP config only after server path, runtime, and env policy are approved.
4. Add dev-database read-only tools only after local-only auth and prompt-injection tests pass.
5. Add local Ollama helper tools only after documenting GPU model inventory and data-handling rules.
6. Re-evaluate write-capable tools separately; default answer should remain no.
