---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: ai-ops
ai_gen: true
---
# Bitcraft Audits

Dedicated folder for audit reports. Active reports live at the root; completed/historical reports move to `archive/`. MCP excludes archives from default context.

## Structure

```text
.github/audits/
  README.md
  archive/                       ← completed/superseded audit reports
  templates/
    audit.template.md            ← shared finding schema and section structure
  AUDIT_<YYYY-MM-DD>_<KIND>.md   ← active audit reports
```

## Kinds

- `SECURITY` — owned by Sec Ops.
- `INTEGRITY` — owned by Sec Ops.
- `AGENTIC` — owned by AI Ops.

## Lifecycle

1. Audit is run via `audit-security`, `audit-integrity`, or `audit-agentic` prompt.
2. Report is written using `templates/audit.template.md`.
3. Findings are converted to inbox items immediately.
4. When all findings are remediated or queued, move the report to `archive/`.
