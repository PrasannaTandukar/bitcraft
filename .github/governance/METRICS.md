---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: ai-ops
ai_gen: true
---
# Bitcraft Change Impact Metrics

CSS, DIS, and CIL are **internal routing signals** used by humans and agents to decide who works on a change, how much validation it needs, and when to escalate. They are not productivity quotas, performance scores, or tracking infrastructure.

No external system (Jira, Bitbucket, dashboards) is required. These scores live inside `plan.yaml` files and in conversation, nowhere else.

## Change Severity Score (CSS)

CSS estimates how severe the change could be if wrong. Score 0-10.

| Score | Meaning |
| --- | --- |
| 0-2 | Local non-runtime docs, prompt wording, or isolated low-risk UI copy. |
| 3-4 | Isolated code path, contained UI behavior, or low-risk test-only change. |
| 5-6 | Shared application behavior, persistence mapping, auth-adjacent code, or multi-file feature work. |
| 7-8 | Authentication, authorization, privacy, data migration, payment-like value flow, deployment, or cross-module contract change. |
| 9-10 | Production incident risk, destructive data operation, credential/security boundary, irreversible migration, or broad architecture change. |

## Dependency Impact Score (DIS)

DIS estimates change radius. Score 0-10.

| Score | Meaning |
| --- | --- |
| 0-2 | One file or one prompt with no runtime dependency. |
| 3-4 | One bounded context or one frontend view. |
| 5-6 | Multiple layers in one feature path, such as Domain/Application/Api or frontend/API contract. |
| 7-8 | Cross-cutting shared service, persistence model, CI/CD, Docker, identity, or model routing. |
| 9-10 | Repository-wide conventions, schema migrations affecting existing data, security posture, or release process. |

## Change Impact Level (CIL)

CIL routes review, validation, and human escalation. Each level has a protocol file in `.github/config/` defining required agents, research, tools, context sources, development order, validation, review, and escalation rules.

| Level | Rule of thumb | Protocol file |
| --- | --- | --- |
| Low | CSS <= 2 and DIS <= 2 | `.github/config/cil-low.yaml` |
| Medium | CSS <= 6 and DIS <= 6 | `.github/config/cil-medium.yaml` |
| High | CSS 7-8 or DIS 7-8 | `.github/config/cil-high.yaml` |
| Critical | CSS >= 9, DIS >= 9, or security/destructive-data override | `.github/config/cil-critical.yaml` |

Security, privacy, irreversible data, credential, production deployment, or Git history risk raises CIL regardless of numeric score.

## Where CIL Lives

- In each plan's `plan.yaml` under `cil:`.
- Referenced verbally during research, planning, implementation, review, and handover.

Humans may override CIL at any time. Agents must reassess when scope expands during implementation.
