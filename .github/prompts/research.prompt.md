---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
description: 'Run Bitcraft discovery or research with repository evidence, official docs, and clearly separated assumptions before planning.'
agent: 'Researcher'
argument-hint: '[research question, technology, model, package, architecture option, or decision]'
---

# Research

Use this when the user wants evidence before choosing a technical direction.

1. Read the Researcher inbox and relevant `.github/governance` guidance.
2. Define the research question and decision it supports.
3. Gather repository evidence and local `docs/dev/` stack evidence first.
4. Use approved external documentation retrieval only when (a) the user grants access in the current chat session, or (b) the source is listed in `.github/config/approved-external-sources.yaml`.
5. For deep research, also use broader web retrieval, collect sources, and report local/remote source discrepancies.
6. Delegate to the human when sources are absent, conflicting, inaccessible, or require approval.
7. Identify best-practice alignment, deviations, risks, and missing information.
8. When the research output becomes a standalone document under `docs/` or `.github/governance/`, emit the provenance line defined in `.github/governance/PROVENANCE.md` as the first line under the H1 title.
9. If follow-up work is needed, create concise inbox items for Planner, Typewriter, AI Ops, Dev Ops, or Sec Ops.

Output: evidence, confidence, recommendation, and follow-up inbox items.
