---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
description: 'Run a Bitcraft security audit using Sec Ops and the shared audit template.'
agent: 'Sec Ops'
argument-hint: '[audit scope, subsystem, release, feature, or security concern]'
---

# Security Audit

Use this for a presentable security and privacy audit.

1. Read `.github/audits/templates/audit.template.md`, `.github/governance/METRICS.md`, and the Sec Ops inbox.
2. Scope identity, auth, authorization, privacy, data governance, Documents, Knowledge, RAG, MCP, prompt injection, secrets, and deployment exposure.
3. Produce findings with severity, CSS, DIS, CIL, evidence, owner, and remediation.
4. Raise Critical items to the human immediately.
5. Create concise inbox items for remediation owners.

Output: audit report or findings summary, critical escalations, and remediation inbox items.
