# Bitcraft Agentic AI Metrics

This file defines decision metrics for Bitcraft's human-agent workflow. Metrics are for learning, routing, and risk control. They are not productivity quotas.

## Human Decisions Needed Before Enforcement

Answer these before metrics become gates rather than guidance:

- Which branch convention should Dev Ops propose first: trunk-based short-lived branches, GitHub-flow style, or another Bitbucket/Jira convention?
- Which Jira project keys and issue types should branch names support?
- Where should metrics be captured first: Jira fields, Bitbucket PR metadata, a local markdown register, or a lightweight script/export?
- What thresholds should require human approval for Change Impact Level High and Critical?
- What counts as an agent-caused incident versus a normal defect discovered during development?
- Should token/cost data be manually estimated, pulled from provider dashboards, or deferred until local telemetry exists?

## Change Impact And Risk Metrics

### Change Severity Score (CSS)

CSS estimates how severe the change could be if wrong. Score 0-10.

| Score | Meaning |
| --- | --- |
| 0-2 | Local non-runtime docs, prompt wording, or isolated low-risk UI copy. |
| 3-4 | Isolated code path, contained UI behavior, or low-risk test-only change. |
| 5-6 | Shared application behavior, persistence mapping, auth-adjacent code, or multi-file feature work. |
| 7-8 | Authentication, authorization, privacy, data migration, payment-like value flow, deployment, or cross-module contract change. |
| 9-10 | Production incident risk, destructive data operation, credential/security boundary, irreversible migration, or broad architecture change. |

### Dependency Impact Score (DIS)

DIS estimates change radius. Score 0-10.

| Score | Meaning |
| --- | --- |
| 0-2 | One file or one prompt with no runtime dependency. |
| 3-4 | One bounded context or one frontend view. |
| 5-6 | Multiple layers in one feature path, such as Domain/Application/Api or frontend/API contract. |
| 7-8 | Cross-cutting shared service, persistence model, CI/CD, Docker, identity, or model routing. |
| 9-10 | Repository-wide conventions, schema migrations affecting existing data, security posture, or release process. |

### Change Impact Level (CIL)

Use CIL to route review and validation.

| Level | Rule of thumb | Minimum action |
| --- | --- | --- |
| Low | CSS <= 2 and DIS <= 2 | Focused self-check. |
| Medium | CSS <= 6 and DIS <= 6 | Relevant expert or Lead Dev review. |
| High | CSS 7-8 or DIS 7-8 | Lead Dev plus affected specialist review. |
| Critical | CSS >= 9, DIS >= 9, or security/destructive-data override | Human escalation before proceeding. |

Security, privacy, irreversible data, credential, production deployment, or Git history risk can raise CIL regardless of numeric score.

### Change Success Rate (CSR)

CSR measures accepted AI-assisted changes that did not require rollback, emergency hotfix, reopened critical defect, or major human rewrite.

Formula:

```text
CSR = successful AI-assisted changes / total AI-assisted changes in the period
```

Track by agent, model family, prompt/skill, and CIL.

### Incident Leakage Rate (ILR)

ILR measures issues that escaped the intended review or validation stage.

Formula:

```text
ILR = escaped incidents or significant defects / total reviewed changes in the period
```

Tag leakage by missed stage: planning, implementation, test, review, security, deployment, documentation, or agentic customization.

### Delivery Stability Metrics

Use DORA-style metrics as a baseline where release data exists:

- Change lead time.
- Deployment frequency.
- Change fail rate.
- Failed deployment recovery time.
- Deployment rework rate.

Do not compare Bitcraft metrics to unrelated projects. Use them to spot local friction and improvement over time.

## Agent Execution And Reasoning Metrics

Track per agent, model family, prompt/skill, and CIL when practical.

| Metric | Definition | Use |
| --- | --- | --- |
| Plan Adherence Rate | Planned steps completed without unjustified divergence. | Detect planner/developer mismatch. |
| Tool Selection Accuracy | Tool calls that were necessary, safe, and scoped divided by total tool calls. | Reduce wasted premium requests and risky tools. |
| Unsupported Claim Rate | Claims not backed by repo evidence, official docs, or clearly marked assumptions. | Reduce hallucination and stale memory drift. |
| Loop Count | Repeated fix/validate cycles for the same defect. | Detect unclear ownership or weak tests. |
| Token And Cost Estimate | Prompt, completion, and total tokens or provider cost where available. | Compare model value by task type. |
| Context Loss Events | Times the agent lost current task state or answered an old request. | Improve context management and summaries. |
| Validation Pass Rate | First focused validation success for touched slice. | Assess edit quality and test fit. |
| Rework Rate | Changes needing substantial human or agent rewrite after review. | Identify skill, prompt, or model mismatch. |
| Inbox Throughput | Items completed, parked, blocked, and aged by agent. | Keep cross-agent backlog healthy. |

## Model And Workflow Metrics

- Model fit by task type: successful changes, rework, token/cost, latency, and user correction rate.
- Local versus cloud routing: privacy fit, cost, quality, speed, and validation outcome.
- Premium request efficiency: successful outcome per premium request or high-cost model turn.
- Escalation accuracy: times a lightweight model correctly escalated versus overreached.
- Junior Dev readiness: tasks local models completed safely with low rework and clear validation.

## Minimum Change Record Schema

Use this schema manually in Jira, Bitbucket PR metadata, or a future metrics register:

```text
date:
work_item:
branch:
human_owner:
agent_owner:
supporting_agents:
model_family:
prompt_or_skill:
css:
dis:
cil:
validation:
review_route:
result: success | rework | blocked | rollback | incident
csr_counted: yes | no
incident_leakage: none | planning | implementation | test | review | security | deployment | documentation | agentic
notes:
```

## Telemetry Alignment

If Bitcraft later instruments agent execution, align naming with OpenTelemetry GenAI semantic conventions where practical: operation, system/provider, model, prompt/completion/total tokens, response status, errors, and MCP tool spans.

For now, keep capture lightweight and human-readable.
