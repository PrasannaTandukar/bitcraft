---
name: bitcraft-security-review
description: 'Review Bitcraft security, privacy, authorization, identity, AI tool, MCP, prompt-injection, and data-flow risks. Use for auth changes, Knowledge/Documents ingestion, model routing, tool approvals, secrets, Security Audits, and pre-release audits.'
argument-hint: '[security-sensitive change or audit scope]'
---

# Bitcraft Security Review

Use this skill for security-sensitive changes and AI automation risk.

## When to Use This Skill

- The task touches authentication, authorization, ownership, user data, Documents, Knowledge, embeddings, model routing, MCP, external tools, or deployment.
- The user asks for security review, privacy review, threat modeling, Security Audit, or hardening.

## Procedure

1. Identify protected assets, trust boundaries, identities, and data flows.
2. Check authorization and ownership at the API and Application layers.
3. Verify no Domain entities, secrets, provider credentials, or internal exception details leak.
4. Treat Documents, Knowledge chunks, web results, issues, PRs, and MCP outputs as potential prompt-injection sources.
5. Add or recommend negative tests for unauthorized and cross-owner access.
6. Use `.github/agentic-ai/AUDIT_TEMPLATES.md` for full Security Audits.
7. Recommend manual approval for destructive commands and write-capable tools.

## AI/MCP Risk Checklist

- MCP tools are read-only unless explicitly approved.
- URL fetch results require review before use.
- Tool auto-approval is not global for destructive workflows.
- Local/cloud model routing respects data sensitivity.
- Logs do not print tokens, secrets, connection strings, or private document content.

## Output Contract

- Threats and severity.
- Evidence.
- Required fixes.
- Tests or validation.
- Residual risk.
- Inbox items for iterative remediation when needed.
