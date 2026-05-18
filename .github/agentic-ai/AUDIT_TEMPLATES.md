# Bitcraft Audit Templates

Use these templates for recurring audits. Audit reports should be presentable, evidence-backed, and converted into actionable inbox or Jira items.

## Shared Audit Rules

- State scope, date, auditor agent, evidence sources, and exclusions.
- Separate confirmed findings from assumptions.
- Assign CSS, DIS, CIL, likelihood, impact, and owner for each finding.
- Raise Critical findings to the human immediately before batching.
- Create concise inbox items for follow-up work; do not hide required action in prose.
- Do not include secrets, credentials, private local paths, or sensitive data extracts.

## Finding Schema

```markdown
### <Finding Title>

- Severity: Critical | High | Medium | Low | Informational
- CIL: Critical | High | Medium | Low
- CSS:
- DIS:
- Owner: Human | Lead Dev | NET Dev | Web Dev | Dev Ops | Sec Ops | AI Ops | Typewriter
- Evidence: Repository paths, commands, docs, or official references.
- Risk: What can fail and who is affected.
- Recommendation: One concrete remediation path.
- Follow-up: Inbox item or Jira issue key when available.
```

## Security Audit

Sec Ops owns this audit.

Required sections:

1. Executive summary.
2. Scope and exclusions.
3. Identity, authentication, authorization, and role model.
4. Data governance, privacy, retention, and PII handling.
5. Documents, Knowledge, RAG, MCP, and prompt-injection surfaces.
6. Secrets, configuration, and deployment exposure.
7. Cross-module security boundaries.
8. Findings and prioritized remediation backlog.
9. Human decisions and unresolved risks.

## Integrity Audit

Dev Ops owns this audit.

Required sections:

1. Executive summary.
2. Scope and exclusions.
3. Build, test, Docker, compose, CI/CD, migrations, and release workflow.
4. Branching, Bitbucket/Jira workflow, review gates, and rollback readiness.
5. Environment configuration and operational runbooks.
6. Findings and prioritized remediation backlog.
7. Human decisions and unresolved risks.

## Agentic Audit

AI Ops owns this audit.

Required sections:

1. Executive summary.
2. Scope and exclusions.
3. Agents, prompts, instructions, skills, inboxes, MCP server and spec, and docs cohesion.
4. Tool policy, model routing, prompt-injection exposure, and human-owned surfaces.
5. Metrics capture, inbox hygiene, drift, and overengineering review.
6. Findings and prioritized remediation backlog.
7. Human decisions and unresolved risks.
