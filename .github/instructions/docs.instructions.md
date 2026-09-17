---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
description: "Bitcraft developer documentation standards for README and docs markdown files."
applyTo: "README.md,docs/**/*.md"
---

# Documentation Standards

- Write developer-facing documentation that describes real repository behavior, not aspirational setup unless clearly labeled as a plan.
- Keep agentic AI operational assets under `.github`; use `docs/` only for human-readable overview, management, and maintenance guidance.
- Prefer concrete paths, commands, validation steps, and ownership notes.
- Keep security, privacy, and operational caveats visible near any workflow that uses external AI tools, MCP, local models, or automation.
- Avoid duplicating detailed prompt or skill bodies in docs; link to the owning `.github` file instead.
- Write docs in STE-flavored Simplified Technical English per `.github/skills/bitcraft-ste/SKILL.md`: short sentences, active voice, simple tenses, no semicolons, no marketing adjectives. Keep technical terms consistent; define them once.
