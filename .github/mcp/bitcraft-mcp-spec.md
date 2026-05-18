# Bitcraft MCP Specification

This file is the long-form MCP design surface. A minimal read-only MCP server is now implemented under `mcp-server/`; this spec covers both the implemented baseline and the deferred extensions.

## Implemented Baseline

`mcp-server/server.py` exposes a small set of read-only tools using the official Python MCP SDK:

- `list_inboxes`, `read_inbox(agent_slug)`
- `list_governance_docs`, `read_governance(name)`
- `list_agents`, `read_roster`

The server runs over stdio and is wired in `.vscode/mcp.json`. All file access is constrained to `.github/agentic-ai/`, `.github/agents/`, and `.github/inboxes/`. No writes, no shell, no network.

When extending the server, follow the Security Model section below and update both this spec and `mcp-server/README.md`.

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