---
name: bitcraft-agentic-stack
description: 'Design, maintain, and refactor Bitcraft GitHub Copilot customizations in .github. Use for prompts, instructions, custom agents, skills, inboxes, MCP specs, metrics, tool policy, context management, and agentic AI workflow cohesion.'
argument-hint: '[customization task or workflow problem]'
---

# Bitcraft Agentic Stack

Maintain the project AI customization system as a layered product.

## When to Use This Skill

- The task touches `.github/copilot-instructions.md`, `.github/instructions`, `.github/prompts`, `.github/agents`, `.github/skills`, `.github/inboxes`, `.github/agentic-ai`, or `.github/mcp`.
- The user asks to improve prompts, agents, skills, context, MCP, or tool usage.
- Customizations conflict, duplicate each other, or drift from the codebase.

## Layer Responsibilities

- Always-on instructions: durable non-negotiable project rules.
- Scoped instructions: file-pattern rules with `applyTo`.
- Prompts: user-invoked workflow entrypoints.
- Agents: role, tool policy, handoffs, and task boundaries.
- Skills: reusable procedures loaded on demand.
- MCP specs: future tool/resource contracts and security policy.

## Procedure

1. Verify VS Code/GitHub Copilot syntax against current docs when adding new primitives.
2. Keep rules short and avoid duplicating long procedures across layers.
3. Use skill descriptions with clear trigger terms so Copilot can discover them.
4. Keep agent tool lists narrow.
5. Add docs only under `docs/` for human-facing overview and maintenance guidance.

## Gotchas

- Use the VS Code `applyTo` field for scoped instructions.
- Skill folder name and `name` field must match exactly.
- VS Code UI handoffs and Bitcraft repository inboxes are different systems; keep both intentional.
- Metrics are guidance until human-approved thresholds and capture location exist.
- Do not place operational agentic AI files outside `.github`.
