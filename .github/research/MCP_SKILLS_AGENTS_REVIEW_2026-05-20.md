---
created_at: 2026-05-20
edited_at: 2026-05-20
creator: ai-ops
editor: ai-ops
ai_gen: true
---
# MCP, Skills, and Agents Review — 2026-05-20

Scope: a focused review against the locked feature-freeze-2026-05-21 v3.1 plan. Identifies (1) MCP tools already aligned with each work-stream, (2) new skill candidates worth scaffolding inside the freeze window, and (3) gaps in the agent roster.

## 1. Existing MCP Coverage Against WS0–WS9

The local `bitcraft-mcp` server (`mcp-server/server.py`, read-only) exposes the following tool surface that is already useful per work-stream:

| WS | Theme | MCP tools already useful |
|----|-------|--------------------------|
| WS0 | Foundations / ERM | `read_plan`, `list_active_plans`, `verify_architecture_readonly` |
| WS1 | Identity / ABAC | `read_governance` (PROJECT_BRIEF, METRICS), `verify_architecture_readonly` |
| WS2 | Bootstrap / config | `read_config` (cil-*.yaml, approved-external-sources.yaml), `list_configs` |
| WS3 | RuntimeEngine / providers | `read_plan` (plan.erm.md), `list_governance_docs` |
| WS4 | Documents / visibility | `verify_architecture_readonly` (Domain isolation checks) |
| WS5 | Knowledge / pgvector | `read_plan`, `read_governance` |
| WS6 | Tutor revisions / activation | `read_plan` |
| WS7 | API contracts | `verify_architecture_readonly` (Application no-package rule) |
| WS8 | Audit / SaveChanges interceptor | `read_governance` (HUMAN_DECISIONS D-10 area) |
| WS9 | Frontend TS migration | `list_doc_stacks`, `read_doc_stack` |

Inbox-routing tools (`list_inboxes`, `read_inbox`, `read_inbox_structured`) and roster tools (`list_agents`, `read_roster`) support cross-WS coordination and are exercised every iteration. The external `fetch` and `git` MCP servers remain read/read-only-write respectively and are not extended by this review.

## 2. New Skill Candidates

Three skills would materially reduce drift during the freeze. Status: **proposed** — awaiting Planner/Lead Dev assignment before scaffolding.

1. **`bitcraft-abac-policy-authoring`** — Guides authoring of the centralized policy evaluator (D1 / D1.a). Inputs: resource type, subject claims, resource attributes (e.g. `Document.Visibility`). Output: a `Result<T>` policy decision and a unit-test template. Useful in WS1, WS4, WS6.
2. **`bitcraft-openapi-client-codegen`** — Codifies the OpenAPI-driven TS client generation lane (D18 / D20). Inputs: backend contract path, target frontend module. Output: codegen invocation, expected diff surface, drift-check guidance. Useful in WS7 and WS9. Pairs with D18.a migration order.
3. **`bitcraft-ef-tph-migration`** — Codifies safe TPH/TPC migration patterns for the `RuntimeEngine` and `Model` hierarchies (D11 / D11.a). Inputs: hierarchy root, discriminator column, reserved-vs-surfaced provider set. Output: migration scaffold checklist and rollback note. Useful in WS3 and WS5.

Each candidate fits the existing `bitcraft-<topic>` naming convention and would live under `.github/skills/<slug>/SKILL.md` with a single-page body. No new tools required.

## 3. Agent Roster Gap Analysis

The 10-agent roster (Secretary, Researcher, Planner, Lead Dev, NET Dev, Web Dev, Dev Ops, Sec Ops, AI Ops, Typewriter) covers the freeze comfortably. No new agent slot is justified inside this freeze window. Observations:

- **Data Ops** style work (migrations, index strategy per D5/D5.a, pgvector tuning) is currently split between NET Dev (schema) and Dev Ops (deployment). This is fine for the PoC; a dedicated agent is **not** recommended now.
- **QA / Test** style work is fully owned by NET Dev and Web Dev with the `bitcraft-test-hardening` skill. No gap.
- **Release Manager** style work is fully owned by Dev Ops with the `bitcraft-devops-release` skill. No gap.
- The `name:` frontmatter standardization audit (planned task T3) found **all 10 agent files already use Title Case `name:` values**. Only [ROSTER.md](../agents/ROSTER.md) needed display-name annotations; no agent file edits were required. A short naming-convention block was added to ROSTER.md.

## 4. Recommended Follow-Ups

- Planner to confirm which of the three skill candidates land inside the freeze versus parking until post-freeze. AI Ops will scaffold accepted skills using the existing `bitcraft-prompt-engineering` skill as a structural reference.
- AI Ops continues to track MCP read-only scope; no write-capable MCP tools proposed in this review.
- Re-review trigger: any change to plan.yaml `revision`, any new work-stream, or any new locked decision that introduces a new resource attribute or provider type.

## References

- [feature-freeze-2026-05-21/plan.yaml](../plans/feature-freeze-2026-05-21/plan.yaml) (v3.1)
- [feature-freeze-2026-05-21/plan.decisions.md](../plans/feature-freeze-2026-05-21/plan.decisions.md)
- [mcp-server/server.py](../../mcp-server/server.py)
- [agents/ROSTER.md](../agents/ROSTER.md)
- [governance/twelve-factor.md](../governance/twelve-factor.md)
