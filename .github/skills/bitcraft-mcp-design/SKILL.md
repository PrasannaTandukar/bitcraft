---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
name: bitcraft-mcp-design
description: 'Design Bitcraft MCP servers, tools, resources, prompts, safety policy, and implementation phases. Use for custom MCP planning, read-only repository context tools, Knowledge/Documents resources, local Ollama helpers, and Copilot MCP integration.'
argument-hint: '[MCP use case, tool, or server scope]'
---

# Bitcraft MCP Design

Use this skill to design MCP capabilities before writing server code.

## When to Use This Skill

- The user asks for custom MCP, tool design, context sources, or efficient agent access.
- A workflow needs read-only access to Bitcraft architecture, docs, test inventory, Knowledge, Documents, or local model metadata.
- You need to decide whether MCP is justified or a prompt/skill is enough.

## Procedure

1. Define the agent problem the MCP server solves.
2. Prefer read-only resources and tools first.
3. Specify inputs, outputs, auth, rate limits, caching, and failure behavior.
4. Identify prompt-injection risks from external or user-authored content.
5. Define a validation harness before implementation.
6. Keep `.vscode/mcp.json` or server code out of scope unless explicitly requested.

## Default Tool Policy

- Safe first tools: repo-map, bounded-context summary, test inventory, contract lookup, domain glossary, docs search, migration status readout.
- Deferred or gated tools: database queries, issue/PR write actions, document ingestion, vectorization triggers, deployment actions.
- Forbidden by default: destructive database updates, file deletion, credential printing, production data exfiltration, and Git writes.

## Output Contract

- MCP goal.
- Server boundary.
- Tools/resources/prompts.
- Security model.
- Implementation phases.
- Validation plan.
