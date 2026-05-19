---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: ai-ops
ai_gen: true
---
# Bitcraft CIL Protocols

Per-CIL-level execution protocols that define agents involved, research required, tool policy, context sources, development order, validation, review, and human-escalation rules.

## Files

- `cil-low.yaml` — low-risk routine changes.
- `cil-medium.yaml` — multi-file or expert-domain changes.
- `cil-high.yaml` — high-blast-radius or security-adjacent changes.
- `cil-critical.yaml` — irreversible, production-affecting, or security-boundary changes.

## Usage

Each plan's `plan.yaml` has a `cil:` field. Agents read the matching protocol before starting work. The human may override `cil:` in `plan.yaml` at any time; agents must reassess if scope expands.

CSS/DIS/CIL definitions live in `.github/governance/METRICS.md`.
