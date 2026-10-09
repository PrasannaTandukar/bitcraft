---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# Bitcraft Copilot Content Exclusion Policy

Bitcraft does not use a `.copilotignore` file because that primitive does not exist. This document records the actual mechanisms available to keep Copilot away from sensitive content, the boundary against `.gitignore`, and the operational rules for Bitcraft contributors.

## What Actually Works In VS Code

1. **`github.copilot.enable` setting.** Per-language and per-scheme switch in VS Code user or workspace settings. Disable Copilot for `plaintext`, `markdown`, or specific schemes when needed. Setting reference: [github.copilot.enable](https://code.visualstudio.com/docs/copilot/reference/copilot-settings#github-copilot-enable).
2. **`files.exclude` and `search.exclude`.** VS Code respects these for indexing and search. Files excluded from search are not sent as workspace context for chat retrieval. Configure in workspace settings.
3. **`.gitignore`.** Files ignored by Git are still readable by VS Code unless they are also covered by `files.exclude`. `.gitignore` is **not** a Copilot exclusion mechanism on its own.
4. **Workspace folder selection.** Multi-root workspaces let you exclude an entire folder by not adding it to the workspace.
5. **Per-tool approval and Configure Tools picker.** For Copilot Chat/agent mode, deny tools that would read sensitive areas.

## What Does Not Apply To Bitcraft

- **GitHub Content Exclusion** (Business/Enterprise feature). Bitcraft is hosted on Bitbucket; GitHub.com content exclusion does not apply to this repository.
- **Copilot CLI permissions.** Bitcraft does not adopt the Copilot CLI (decision 2026-05-19). The `permissions-config.json` mechanism is not in scope.
- **Hypothetical `.copilotignore` file.** Not a real Copilot primitive. Do not create one.

## Bitcraft Operational Rules

1. **No secrets in repository.** Secrets, tokens, dataset paths, model weights, and production endpoints are excluded by `.gitignore` (`*.env`, `.env.local`, `.env.*.local`, `.conda/`, etc.) and must never enter the working tree.
2. **Agent inboxes and sensitive ad-hoc files are local-only.** `.gitignore` blocks `.github/inboxes/`, `*.session.md`, `*.draft.md`, and `*.transcript.md` so agents can use transient coordination without committing it.
3. **Developer-local documentation.** `docs/dev/` is ignored by Git and populated locally by humans/developers. Agents may read it only when the active workflow calls for local documentation lookup, and must tolerate it being absent.
4. **Identity, customer, and tutor user content.** None of this lives in the repository today. If it ever does, the path must be added to both `.gitignore` and to workspace `files.exclude`/`search.exclude` so Copilot does not retrieve it for context.
5. **MCP servers must enforce their own scope.** The repository read-only MCP server constrains tools to approved repository governance, agent, inbox, and developer-local documentation paths. New MCP tools must follow the same pattern (no free-form paths).
6. **No exclusion theater.** Do not add files to `files.exclude` purely to "hide them from Copilot" if they are not actually sensitive. The signal value of the list collapses if it is full of noise.

## Reflection

Revisit this policy when:

- Bitcraft adopts GitHub-hosted mirroring (would re-enable GitHub Content Exclusion).
- Tutor user data enters the repository (it should not).
- VS Code introduces new exclusion primitives.
- A new MCP server widens the file access surface.

Ownership: AI Ops, with Sec Ops review on any change affecting sensitive content paths.
