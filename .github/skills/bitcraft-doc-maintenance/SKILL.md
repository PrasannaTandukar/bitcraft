---
created_at: 2026-05-19
edited_at: 2026-09-17
creator: ai-ops
editor: ai-ops
ai_gen: true
name: bitcraft-doc-maintenance
description: "Create and maintain Bitcraft developer documentation and reports grounded in verified repository behavior. Use for README updates, docs/ guides, agentic AI overview docs, setup instructions, architecture notes, audit writeups, and maintenance playbooks."
argument-hint: "[doc topic, report, README section, or file]"
---

# Bitcraft Documentation Maintenance

Use this skill to write docs and reports that stay aligned with the code and AI customization stack. Runs primarily inside the Secretary agent.

## When to Use This Skill

- The user asks for README, docs, setup, architecture, report, or maintenance documentation.
- A code or `.github` change needs human-facing explanation.
- Research or audit artifacts need consolidation into durable documentation.

## Procedure

1. Read `.github/instructions/docs.instructions.md` and the Secretary inbox.
2. Read the specific files that describe the feature being documented. Verify the documented behavior against repository files.
3. Put operational AI customization files under `.github`; put human-facing overviews under `docs/`.
4. Link to owning files instead of duplicating long agent or skill bodies.
5. Make docs concrete: paths, commands, validation steps, ownership notes.
6. When the deliverable is a new or fully rewritten standalone document under `docs/` or `.github/governance/`, emit the provenance line from `.github/governance/PROVENANCE.md` as the first line under the H1 title.
7. Apply `bitcraft-ste` (STE-flavored mode) to the prose before shipping.
8. Update inboxes for follow-up docs work.

## Documentation Checklist

- Audience is clear.
- Current state is evidence-backed.
- Commands and paths are concrete.
- Security and privacy caveats sit near automation and AI tools.
- Maintenance owner or cadence is defined for volatile areas.
- Future plans are marked as planned work, not current state.

## Gotchas

- Do not invent behavior or present plans as implemented state.
- Do not write final external/legal/customer deliverable prose as if human-approved.
- Do not leave root-level agentic AI research files as competing sources of truth.
