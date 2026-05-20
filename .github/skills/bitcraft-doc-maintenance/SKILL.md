---
name: bitcraft-doc-maintenance
description: 'Create and maintain Bitcraft developer documentation that reflects actual repository behavior. Use for README updates, docs/ guides, agentic AI overview docs, setup instructions, architecture notes, and maintenance playbooks.'
argument-hint: '[doc topic or file]'
---

# Bitcraft Documentation Maintenance

Use this skill to write docs that stay aligned with the code and AI customization stack.

## When to Use This Skill

- The user asks for README, docs, setup, architecture, or maintenance documentation.
- A code or `.github` change needs human-facing explanation.
- Research artifacts need to be consolidated into durable documentation.

## Procedure

1. Verify the documented behavior against repository files.
2. Put operational AI customization files under `.github`; put human-facing overviews under `docs/`.
3. Link to owning files instead of duplicating long prompt, agent, or skill bodies.
4. Include validation and maintenance steps.
5. Mark future plans clearly as planned work, not current state.

## Documentation Checklist

- Audience is clear.
- Current state is evidence-backed.
- Commands and paths are concrete.
- Security and privacy caveats are near automation and AI tools.
- Maintenance owner or cadence is defined for volatile areas.

## Gotchas

- Do not leave root-level agentic AI research files as competing sources of truth.
- Do not document speculative MCP tooling as already implemented.
