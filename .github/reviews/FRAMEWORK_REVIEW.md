---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# Bitcraft Agentic Framework Review

## 2026-05-19 Iteration: MCP Expansion, Provenance, Static Linting

### Trigger

User requested a planning and implementation iteration for the agentic AI stack covering MCP additions, prompt-management hygiene, model routing refinement, content-exclusion clarification, and provenance. Later in the same iteration, the user rejected in-repository LLM prompt-evaluation tooling as context bloat.

### Research Checks

- `.copilotignore` is not a real Copilot primitive. The actual mechanisms are `github.copilot.enable`, `files.exclude`/`search.exclude`, GitHub Content Exclusion (not applicable to this Bitbucket-hosted repo), and `.gitignore`.
- Copilot CLI hooks (`preToolUse`/`postToolUse`) are scoped to the CLI and cloud agent runtime, not VS Code Chat.
- The `gh copilot` extension is retired; the new CLI is `copilot`.
- No official Bitbucket MCP server exists; GitHub MCP and Atlassian Remote MCP exist but require OAuth flows.
- VS Code MCP `sandboxEnabled` is macOS/Linux only; on Windows compensating controls are per-tool approval and Configure Tools.
- Copilot's [model-comparison reference](https://docs.github.com/en/copilot/reference/ai-models/model-comparison) categorizes models as general-purpose, fast/lightweight, deep reasoning, and visual.

### Decisions

- Drop Copilot CLI adoption from scope. VS Code Copilot only.
- Skip GitHub and Atlassian MCP servers. Bitbucket integration deferred until an official MCP exists.
- Add `fetch` and read-only `git` MCP servers via `uvx`.
- Add `verify_architecture_readonly` tool to `mcp-server/server.py` as a read-only Clean Architecture spot check.
- Author `.github/governance/CONTENT_EXCLUSION.md` to replace the `.copilotignore` misconception.
- Author `.github/governance/PROVENANCE.md` and emit a provenance blockquote from `compact`, `write`, and `research` prompts on standalone reports.
- Refresh `MODEL_ROUTING.md` with task categories, Secretary-as-triage / Lead-Dev-as-orchestrator separation, and a hybrid cascade default. No version pinning.
- Remove the `.github/prompt-eval/` scaffold and promptfoo config. Keep only static linting helpers: root `.markdownlint.jsonc`, root `.vale.ini`, and `.github/vale/styles/`.
- Add `.github/governance/PROMPT_STACK_MAINTENANCE.md` for prompt-stack methodology, ambiguity controls, and external evaluation options.
- Extend `.vscode/extensions.json` with markdownlint, Vale, Python, ESLint, Prettier, Copilot/Copilot Chat. No MCP Inspector entry (unverified publisher).

### Best-Practice Deviations Or Caveats

- The `verify_architecture_readonly` MCP tool only **reports** violations. Real enforcement still belongs to tests and CI.
- `fetch` and `git` MCPs require the `uv` toolchain on the developer machine. Documented in `mcp-server/README.md`.
- Static linting does not measure prompt effectiveness. Prompt or agent quality evaluation should happen in a separate evaluation project or external toolchain after curated examples exist.
- Sandbox-based MCP isolation is unavailable on Windows; we rely on per-tool approval.

### Follow-Up Work

- Sec Ops: review `fetch` (prompt-injection on returned web content) and `git` (must remain read-only).
- Typewriter: refresh `README.md`, `docs/agentic-ai.md`, `docs/agentic-setup-guide.md` to reflect new MCP servers, static linting commands, provenance convention, and CONTENT_EXCLUSION policy.

## 2026-09-18 Tracking Boundary

- Keep `.github/inboxes/` local-only and ignored by Git.
- Track durable plans, governance, audits, research, reviews, skills, instructions, and agent definitions.
- Move durable decisions and outcomes from local inboxes into tracked static documents.
- Treat missing inboxes as valid after clone or workspace reset.
- AI Ops: refresh `/memories/repo/` entries to reflect the new file layout (`PROVENANCE.md`, `CONTENT_EXCLUSION.md`, `.github/vale/styles/`, `verify_architecture_readonly` tool).
- AI Ops: run the next Agentic Audit once these changes have been exercised in at least one workflow.

## 2026-05-18 Cohesion Reset

### Trigger

The previous stack had become role-heavy and did not yet have persistent inboxes, metrics-backed decision routing, or a clear model for human and agent cooperation.

### Research Checks

- VS Code customization docs support project custom agents in `.github/agents`, prompt files in `.github/prompts`, skills in `.github/skills`, and file-based instructions with `applyTo`.
- VS Code custom agents support UI handoffs, but the then-persistent inbox system was a Bitcraft repository convention rather than a native Copilot primitive.
- GitHub repository instructions support `.github/copilot-instructions.md`, `*.instructions.md`, and `AGENTS.md` style agent guidance.
- OpenTelemetry GenAI semantic conventions are a useful future alignment point for model, operation, token, error, and MCP span metrics, but local manual capture is enough now.
- DORA delivery metrics are useful for delivery health, but should be used for local improvement rather than targets or cross-project comparison.
- Trunk-based development and short-lived branches align better with agent-assisted work than long-lived per-agent branches, pending human approval of Bitbucket/Jira conventions.

### Decisions

- Replace the fragmented agent roster with 10 short role names grouped into Admin, Planning, Development, and Integrity departments.
- Make Lead Dev responsible for development delegation and fallback implementation.
- Make AI Ops the owner for prompts, instructions, agents, skills, MCP, metrics, model routing, and agentic audits.
- Add repository-backed inboxes under `.github/inboxes` and keep VS Code UI handoffs as optional next-step buttons.
- Add metrics definitions and review routing under `.github/governance/METRICS.md`.
- Keep exact model names out of frontmatter until the active Copilot client verifies availability.

### Best-Practice Deviations Or Caveats

- The former persistent inboxes were not a VS Code-native primitive. They were simple Markdown queues used to reduce coordination overhead and premium model calls.
- Some read-heavy agents have edit access so they can maintain their own inboxes and owned reports. Their prompts restrict source edits unless explicitly requested.
- Metrics are guidance until the human approves thresholds and capture location. Hard gates without human agreement would create false precision.

### Follow-Up Work

- Dev Ops should propose Bitbucket/Jira-compatible branch conventions.
- Secretary should gather human answers for metrics thresholds and capture location.
- AI Ops should run the first agentic audit after the new stack is loaded in VS Code.

## 2026-05-18 Cohesion Pass 2

### Trigger

User requested a best-practice review against the [GitHub Copilot customization cheat sheet](https://docs.github.com/en/copilot/reference/customization-cheat-sheet) and [agents.md](https://agents.md/) standard, plus better MCP, context management, and naming cohesion.

### Research Checks

- Customization cheat sheet confirms `.github/copilot-instructions.md`, `.github/instructions/*.instructions.md`, `.github/agents/<name>.md`, `.github/prompts/*.prompt.md`, `.github/skills/<name>/SKILL.md`, and `mcp.json` as supported primitives across VS Code, GitHub, and Copilot CLI.
- agents.md standard places `AGENTS.md` at repository root for cross-tool compatibility (Codex, Aider, Junie, Cursor, Gemini CLI, OpenCode, Copilot cloud agent, Devin).
- VS Code MCP supports `stdio` and `http` servers via `.vscode/mcp.json` or workspace/user settings.

### Decisions

- Renamed `.github/handoffs/` to `.github/inboxes/` so the folder name no longer collides with the `handoffs:` frontmatter block on custom agents.
- Renamed `.github/agents/AGENTS.md` to `.github/agents/ROSTER.md` and added a real `AGENTS.md` at the repository root per the agents.md standard.
- Added a nested `tutor-studio/AGENTS.md` so frontend-only coding agents have local guidance.
- Implemented a minimal read-only MCP server under `mcp-server/` exposing inboxes, roster, and governance docs.
- Added `.github/governance/MODEL_ROUTING.md` as a one-page routing reference for GPT, Claude, Gemini, and local Ollama (DeepSeek, Qwen).
- Added `.github/prompts/compact.prompt.md` so long sessions can be compacted into prioritized inbox items.
- Updated `bitcraft-context-management` skill to integrate the runtime memory scopes (`/memories/`, `/memories/session/`, `/memories/repo/`).
- Updated `.gitignore` to selectively un-ignore `.vscode/mcp.json`, `settings.json`, `extensions.json`, `tasks.json`, `launch.json`, and to ignore agent scratch artifacts (`*.session.md`, `*.draft.md`, `*.transcript.md`).
- Clarified inbox concept as a prioritized batched backlog with cross-agent delegation and Secretary-led aggregation.

### Best-Practice Deviations Or Caveats

- The MCP server is intentionally minimal. Extensions in `bitcraft-mcp-spec.md` (bounded context summaries, contract lookup, test inventory) remain deferred.
- `.github/governance/` retains a project-specific name rather than the more generic `.github/governance/`. The deviation is documented for cross-project reuse.

### Follow-Up Work

- AI Ops: run an audit using `.github/audits/templates/audit.template.md` and capture findings.
- AI Ops: refresh `/memories/repo/` entries to point at the new file layout when next activated.
