---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: ai-ops
ai_gen: true
---
# Bitcraft Research

Dedicated folder for research deliverables. Active reports at the root; completed/superseded reports move to `archive/`. MCP excludes archives from default context.

## Structure

```text
.github/research/
  README.md
  archive/                                ← completed/superseded research reports
  templates/
    research.template.md                  ← standard research note shape
  RESEARCH_<YYYY-MM-DD>_<TOPIC_SLUG>.md   ← active research reports
```

## Lifecycle

1. Research is run via the `bitcraft-research` skill (inside Secretary or Planner).
2. Report is written using `templates/research.template.md`.
3. Recommendations are converted to inbox items.
4. When the research is no longer the active reference (decision made or superseded), move the report to `archive/`.
