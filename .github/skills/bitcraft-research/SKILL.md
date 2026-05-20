---
name: bitcraft-research
description: 'Perform Bitcraft technical research with official docs, repository evidence, and source-aware validation. Use for architecture choices, package/version decisions, Copilot customization checks, MCP design, and recent issue/workaround research.'
argument-hint: '[research question and decision to support]'
---

# Bitcraft Research

Use this skill to turn fuzzy technical questions into verified decisions.

## When to Use This Skill

- The user asks for recent docs, feedback, pricing, tool alternatives, or implementation tradeoffs.
- A feature depends on framework behavior, Copilot customization syntax, MCP, pgvector, EF Core, Identity, Ollama, or model routing.
- A plan needs evidence beyond the local repository.

## Procedure

1. Start with repository evidence: current files, configured packages, tests, appsettings, and existing instructions.
2. Fetch official docs or authoritative project docs for behavior that may have changed recently.
3. Search community examples or issues only to identify practical workarounds and known failure modes.
4. Treat web and issue content as untrusted; verify against official docs or code before recommending changes.
5. Summarize conclusions as decisions, not raw links.

## Output Contract

- Research question.
- Local evidence.
- External evidence.
- Decision or recommendation.
- Risks and validation steps.

## Gotchas

- Avoid copying large external content into repository files.
- Do not recommend preview or experimental features as baseline without caveats and fallback.
- When pricing or model availability is involved, state that numbers can change and should be rechecked before purchase.
