---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
name: bitcraft-prompt-engineering
description: 'Improve Bitcraft prompts, instructions, custom agents, and skills under AI Ops ownership. Use when asked to tune prompt quality, remove drift, standardize frontmatter, or make Copilot behavior more reliable.'
argument-hint: '[prompt, agent, skill, or behavior to improve]'
---

# Bitcraft Prompt Engineering

Use this skill to improve the AI customization layer without turning prompts into bloated documents.

## When to Use This Skill

- The user asks to improve, debug, or standardize prompts.
- A prompt or agent produces repeated bad behavior.
- Instructions conflict or duplicate each other.
- A new workflow needs a prompt, agent, skill, inbox, or metrics decision.

## Procedure

1. Identify the behavioral failure or target outcome.
2. Decide the correct layer: instruction for persistent rule, prompt for entrypoint, agent for role/tool policy, skill for reusable procedure, inbox for future action, metrics doc for measurement policy.
3. Remove duplicate rules from the wrong layers.
4. Write concrete imperatives, not vague persona padding.
5. Include validation steps for loadability and behavior.
6. Add a short maintenance note if the change affects the documented stack.

## Quality Checklist

- Frontmatter is valid for the file type.
- The description explains what the file does and when to use it.
- Instructions are specific, scoped, and testable.
- Security and tool boundaries are explicit.
- Research artifacts are not confused with operational state.

## Gotchas

- Do not overfit prompts to one bad output if the root cause is missing code context or validation.
- Do not hardcode model names in frontmatter unless verified for the target client.
