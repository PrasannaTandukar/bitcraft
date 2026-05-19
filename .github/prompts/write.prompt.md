---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
description: 'Write or update Bitcraft developer documentation and reports through Typewriter, grounded in verified repository behavior.'
agent: 'Typewriter'
argument-hint: '[README, guide, report, audit writeup, docs topic, or summary]'
---

# Write

Use this for docs, reports, guides, README updates, and audit prose.

1. Read `.github/instructions/docs.instructions.md` for docs work and the Typewriter inbox.
2. Read the specific files that describe the feature or component being documented.
3. Keep operational agentic assets under `.github` and human-facing docs under `docs/`.
4. Make docs concrete: paths, commands, validation, ownership, caveats.
5. When the deliverable is a new or fully rewritten standalone document under `docs/` or `.github/governance/`, emit the provenance line defined in `.github/governance/PROVENANCE.md` as the first line under the H1 title.
6. Update inboxes for follow-up docs work.

Output: docs changed, evidence used, validation, and human review needs.
