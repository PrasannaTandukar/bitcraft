---
created_at: 2026-05-19
edited_at: 2026-09-17
creator: ai-ops
editor: ai-ops
ai_gen: true
name: bitcraft-agentic-stack
description: "Design, maintain, refactor, and audit the Bitcraft agentic AI stack in .github: agents, skills, instructions, inboxes, MCP specs, tool policy, model-routing docs, and prose quality. Use for agentic stack changes, customization conflicts or drift, MCP tool design, prompt/skill/agent quality tuning, and agentic audits."
argument-hint: "[customization task, workflow problem, MCP use case, or audit scope]"
---

# Bitcraft Agentic Stack

Maintain the project AI customization system as a layered product. Runs inside the AI Ops agent.

## Layer Responsibilities

- Always-on instructions (`copilot-instructions.md`): durable non-negotiable project rules. Keep small.
- Scoped instructions (`.github/instructions/`): file-pattern rules with `applyTo`.
- Agents (`.github/agents/`): role, tool policy, and task boundaries for the five-agent roster.
- Skills (`.github/skills/`): reusable procedures loaded on demand; skills are the workflow entrypoints (prompt files were retired 2026-09-17).
- Inboxes (`.github/inboxes/`): per-agent future work only.
- MCP specs (`.github/mcp/`): read-only tool/resource contracts and security policy.

## Stack Maintenance Procedure

1. Verify VS Code Copilot customization syntax against current official docs when adding new primitives. Do not rely on training-data memory of frontmatter fields.
2. Keep rules short. Do not duplicate long procedures across layers.
3. Write skill descriptions with clear trigger terms so the model can discover them.
4. Keep agent tool lists narrow.
5. Apply the `bitcraft-ste` skill to all agent-facing prose: active voice, one instruction per sentence, no hedge stacking, one word per meaning.
6. Put human-facing overview docs under `docs/` only.

## Prompt/Skill Quality Procedure

1. Identify the behavioral failure or target outcome.
2. Decide the correct layer: instruction for a persistent rule, agent for role/tool policy, skill for a reusable procedure, inbox for future action.
3. Remove duplicate rules from the wrong layers.
4. Write concrete imperatives, not vague persona padding.
5. Include validation steps for loadability and behavior.

Quality checklist: valid frontmatter for the file type; description explains what and when; instructions specific and testable; tool boundaries explicit; no hardcoded model names in frontmatter.

## MCP Design Procedure

1. Define the agent problem the MCP server solves.
2. Prefer read-only resources and tools first.
3. Specify inputs, outputs, auth, rate limits, caching, and failure behavior.
4. Identify prompt-injection risks from external or user-authored content.
5. Define a validation harness before implementation.
6. Keep `.vscode/mcp.json` and server code changes out of scope unless explicitly requested — modifying `.vscode/mcp.json` is an autopilot-locked action.

Default MCP tool policy: read-only repository context tools are safe first candidates. Database queries, ingestion triggers, deployment actions, and Git writes are forbidden by default.

## Agentic Audit Procedure

1. Read `.github/audits/templates/audit.template.md`, `.github/governance/METRICS.md`, and the AI Ops inbox.
2. Check customization syntax, role overlap, tool policy, model-routing assumptions, inbox hygiene, docs alignment, and stale files.
3. Run static checks: markdownlint and Vale per `.github/governance/PROMPT_STACK_MAINTENANCE.md`.
4. Produce findings with severity, CSS, DIS, CIL, evidence, owner, and remediation.
5. Create inbox items for the owning agents.

## Gotchas

- Skill folder name and `name` field must match exactly; lowercase letters, numbers, hyphens only.
- VS Code UI handoffs and Bitcraft repository inboxes are different systems; keep both intentional.
- Metrics are guidance until human-approved thresholds and capture location exist.
- Do not place operational agentic AI files outside `.github`.
