---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: planner
editor: planner
ai_gen: true
---
# Plan: Agentic Stack Batch 2 — Lean Metrics, CIL Protocols, Plans Persistence, Security Hardening, and Prompt Efficiency

> Origin: Planner, 2026-05-19. Human review: required (see Open Decisions).

## Goal

Deliver a focused second-batch improvement to the agentic AI stack covering:

1. Strip `METRICS.md` to CSS/DIS/CIL for internal routing only. Remove all Jira/tracking-dependent metrics.
2. Introduce lean CIL protocol files (YAML) that define per-level agent selection, research requirements, tool policy, context sources, development order, and validation gates.
3. Introduce `.github/plans/` persistence structure: branch-named plan folders, per-plan YAML config, context markdown, archive subfolders; extend MCP to serve only active plans.
4. Install and confirm FastMCP dependency (clarify standalone vs mcp-package FastMCP).
5. Research Vale, MCP, and linter security community configurations and apply fortifications.
6. Audit and fix prompt vagueness — fixable items now; human-confirmation items flagged.
7. Validate overall agentic architecture against current best practices.
8. Run a focused security audit; fix all Critical and High findings; queue Medium and below.
9. Handle open agent backlog.
10. Produce a clean human handover.

## Non-Goals

- No changes to the backend (.NET, EF Core, domain model).
- No changes to `tutor-studio/`.
- No Jira/Bitbucket integration in this batch.
- No Copilot CLI hooks.
- No write-capable MCP tools.

## Assumptions

- The user confirmed no Jira metric tracking. CSS/DIS/CIL are retained as internal routing signals only.
- "FastMCP" refers to the `mcp.server.fastmcp.FastMCP` class already in use. The `mcp>=1.2.0` package satisfies this. Standalone `fastmcp` PyPI package is not yet confirmed — see Open Decisions.
- Branch-name folders inside `.github/plans/` will start with a single placeholder until the human names the first branch.
- Security research must be completed before security fixes are applied.

---

## Work Stream 1: METRICS.md Simplification

### Current State

`METRICS.md` contains CSS/DIS/CIL definitions, CSR, ILR, DORA delivery metrics, agent execution table, model/workflow metrics, minimum change record schema, OpenTelemetry alignment, and a "Human Decisions Needed" section referencing Jira.

### Decision

Retain only CSS, DIS, CIL, and their definitions. Strip all metrics that require external capture, Jira fields, PR metadata, or manual tracking infrastructure. The metrics file becomes a routing reference, not a measurement program.

### What Is Kept

- CSS scale (0–10) and its table.
- DIS scale (0–10) and its table.
- CIL table (Low/Medium/High/Critical) with rules and minimum actions.
- A brief statement that CSS/DIS/CIL are internal routing signals, not productivity targets.
- A reference to the CIL protocol files (Work Stream 2).

### What Is Removed

- CSR formula and definition.
- ILR formula and definition.
- Delivery Stability Metrics (DORA).
- Agent Execution And Reasoning Metrics table.
- Model And Workflow Metrics list.
- Minimum Change Record Schema.
- Telemetry Alignment section.
- "Human Decisions Needed Before Enforcement" section (questions answered by this batch or deferred to Open Decisions).

### Owner: AI Ops

### CIL: Low (CSS=2, DIS=5 — references in routing docs and prompts)

---

## Work Stream 2: CIL Protocol Files

### Current State

CIL exists as a table in `METRICS.md` but has no machine-readable protocol defining what each level requires in practice.

### Design

Create `.github/config/` folder with four YAML files — one per CIL level. Each file defines:

```yaml
level: low | medium | high | critical
description: One-sentence summary of scope.
agents_required:           # which agents must be involved
research_required:         # what evidence must be gathered before starting
tools_allowed:             # permitted tool categories
context_sources:           # which files/surfaces must be read before work begins
development_order:         # sequence of sub-tasks
validation_required:       # what validation must pass before handoff
review_required:           # which agents must sign off
human_escalation:          # whether human must approve before or after implementation
notes:                     # caveats and override conditions
```

### Usage

- `plan.yaml` inside each plan folder (Work Stream 3) carries a `cil:` field computed at task creation.
- Agents consult the matching `.github/config/cil-<level>.yaml` for process guidance.
- Human can override the computed CIL in `plan.yaml` before work starts.
- CIL should be reassessed when scope changes or new risk surfaces.

### Files

- `.github/config/cil-low.yaml`
- `.github/config/cil-medium.yaml`
- `.github/config/cil-high.yaml`
- `.github/config/cil-critical.yaml`

### Owner: AI Ops

### CIL: Low (CSS=2, DIS=4)

---

## Work Stream 3: Plans Persistence Structure

### Design

```
.github/
  plans/
    archive/                    ← completed plan folders (whole-branch archives)
    <branch-name>/              ← one folder per active branch/task
      archive/                  ← completed plan files within this branch
      plan.yaml                 ← lean config (human-modifiable)
      context.md                ← generated during development, file refs + scope
```

### `plan.yaml` Schema (lean, human-modifiable)

```yaml
# plan.yaml — lean task configuration
branch: <branch-name>
created_at: YYYY-MM-DD
updated_at: YYYY-MM-DD
status: active | blocked | complete
goal: One concise sentence.
cil: low | medium | high | critical   # computed, human can override
css: 0-10
dis: 0-10
agents:
  lead: lead-dev
  supporting:
    - net-dev
    - ai-ops
model_family: claude | gpt | gemini | ollama
notes: Optional free-text.
```

### `context.md` Schema

Short generated file (not a prompt). Created when development begins. Contains:
- Scope sentence.
- List of key files with roles.
- Active sub-tasks.
- Blockers.

### Archive Rules

- When a sub-task file is complete, move it to `<branch>/archive/`.
- When all files in a branch folder are archived, move the whole `<branch>/` folder to `plans/archive/`.
- MCP returns only non-archived plans in normal context.

### MCP Extensions

Two new tools in `mcp-server/server.py`:

1. `list_active_plans()` — returns `plan.yaml` metadata for every `<branch>/` folder not under `archive/`.
2. `read_plan(branch_slug)` — returns `plan.yaml` + `context.md` for one active branch.

Archived plans are invisible to normal tool calls. A separate optional parameter `include_archived=True` on `list_active_plans()` surfaces them only when explicitly requested.

### `.gitignore`

No changes needed — `plans/` is a tracked static-document surface. Inboxes are local-only and ignored by Git. `plans/archive/` is also tracked.

### Owner: AI Ops (MCP extension), Dev Ops (folder structure review)

### CIL: Low (CSS=2, DIS=4)

---

## Work Stream 4: FastMCP Dependency Confirmation

### Current State

`mcp-server/server.py` already imports `from mcp.server.fastmcp import FastMCP` and `requirements.txt` specifies `mcp>=1.2.0`. The `FastMCP` class inside the `mcp` package **is FastMCP** — the server is already using it.

### Options

- **Option A (confirmed current):** The `mcp>=1.2.0` package's built-in `mcp.server.fastmcp.FastMCP` is sufficient. No additional package needed.
- **Option B:** Migrate to the standalone `fastmcp` PyPI package by jlowin/fastmcp for a higher-level framework. Requires changing the import and adding `fastmcp` to `requirements.txt`.

### Recommendation

Option A is sufficient. The existing dependency already provides `FastMCP`. No change required unless the human explicitly wants the jlowin/fastmcp standalone package.

### Human Decision Required

See Open Decisions item 1.

### CIL: Low (CSS=1, DIS=1)

---

## Work Stream 5: Vale, MCP, and Linter Security Research

### Objective

Research community configurations and security best practices for:
1. **Vale**: community style packages, injection-safe rule authoring.
2. **MCP security**: prompt injection prevention, output sanitization, context poisoning mitigations.
3. **Markdownlint**: effective community configurations.

### Scope

- Find GitHub repositories or standards that define safe MCP server patterns.
- Find Vale community packages beyond built-in styles (e.g., Microsoft, Google, Red Hat, errata-ai write-good).
- Identify whether there are any known MCP content-injection attack patterns with documented countermeasures.
- Research how to sanitize LLM-returned content used in agent context (MCP tool output).

### Owner: Researcher (web retrieval required — human must explicitly grant)

### Deliverable

Research note in `.github/governance/RESEARCH_MCP_VALE_SECURITY.md`, findings fed into:
- `mcp-server/server.py` output sanitization.
- Vale style enhancements.
- `bitcraft-mcp-spec.md` security model update.

### CIL: Low (CSS=2, DIS=3)

---

## Work Stream 6: Prompt Vagueness Audit and Fixes

### Findings

After reviewing all 11 prompts:

#### Fixable Without Human Input

| File | Issue | Fix |
| --- | --- | --- |
| `plan.prompt.md` | "Verify current state from nearby repository evidence" — "nearby" is undefined | Replace with: "Verify current state by reading referenced files, inbox items, and relevant source files." |
| `overview.prompt.md` | "Inspect only the relevant repo surface needed for the requested focus" — "relevant" undefined | Replace with: "Inspect inbox items, governance docs, and the specific surface area matching the requested focus." |
| `develop.prompt.md` | "Implement directly only when that is the smallest correct path" — decision boundary unclear | Replace with: "Implement directly only for single-file, self-contained changes that do not require a specialist; delegate all others." |
| `write.prompt.md` | "Gather only enough repository evidence to avoid inventing behavior" — subjective | Replace with: "Read the specific files that describe the feature or component being documented." |
| `review.prompt.md` | "Route specialist review when needed" — undefined criteria | Replace with: "Route to Sec Ops for auth/privacy/data, Dev Ops for deployment/infra, AI Ops for agentic stack, when CIL is Medium or above." |
| `metrics.prompt.md` | Step 3 "Present questions together before enforcing new thresholds or capture locations" — references enforcement which no longer applies after WS1 | Update to reflect simplified CSS/DIS/CIL-only purpose after METRICS.md is simplified. |

#### Requires Human Confirmation Before Fixing

| File | Issue | Human Question |
| --- | --- | --- |
| `research.prompt.md` | "Use approved external documentation retrieval only when the human explicitly grants it" — unclear what "explicitly grants" means in practice. Does a chat mention suffice? Session-level? First-time flag? | See Open Decisions item 2. |
| `audit-security.prompt.md` | Step 2 lists scope without risk ordering; agents may treat as flat checklist | Minor — can be fixed now as "Prioritize findings by CSS/CIL severity" but confirm if ordering matters. |

### Owner: AI Ops

### CIL: Low (CSS=2, DIS=3)

---

## Work Stream 7: Agentic Architecture Validation

### Current State (from AUDIT_2026-05-18_AGENTIC.md)

Stack is cohesive after Cohesion Pass 2. All Copilot primitives are implemented or intentionally deferred. No Critical or High findings. Four outstanding findings (F-1 through F-4) are all Low or Informational.

### Validation Scope For This Batch

Validate the following after batch changes are applied:

1. CIL protocols: are they referenced consistently across `copilot-instructions.md`, `DEVELOPER_GUIDE.md`, and prompt files?
2. Plans structure: does the MCP extension introduce any path-traversal or information-leakage risk?
3. METRICS.md strip: are there downstream references to removed metrics (CSR, ILR, DORA) in prompts, instructions, or skills that need updating?
4. Prompt fixes: do the updated prompts remain internally consistent with each other?
5. FastMCP: is the dependency correctly pinned and documented?

### Owner: AI Ops (post-implementation check via `audit-agentic` prompt)

### CIL: Low (CSS=2, DIS=5)

---

## Work Stream 8: Security Audit and Critical Fixes

### Known Pre-Existing Issues

From Sec Ops inbox and 2026-05-18 audit:

| Item | Severity | Status | Fix |
| --- | --- | --- | --- |
| `verify_architecture_readonly` flags all PackageReference in Application | Low | Active (AI Ops inbox) | Add allow-list for accepted abstraction-only packages (`MediatR`, `Microsoft.Extensions.*`) |
| `fetch` MCP prompt injection | Medium | Blocked on human decision | After human confirms control, document in `bitcraft-mcp-spec.md` |
| `git` MCP tool enumeration | Medium | Blocked on human | Human runs `uvx mcp-server-git` and enumerates tools; then Sec Ops confirms only read-only tools approved |

### New Audit Scope

Run a full security audit after WS5 research completes. Focus:

- MCP output sanitization (new additions from WS3 plans structure).
- Vale rule injection safety.
- CIL protocol YAML — ensure no executable content paths.
- Prompt content — any prompt injection vectors via user-supplied `argument-hint`.
- Path traversal in new `list_active_plans` and `read_plan` MCP tools.

### Fix Sequence

1. Apply `verify_architecture_readonly` allow-list fix (unblocked, AI Ops can do this now).
2. After research (WS5) delivers findings, Sec Ops applies MCP output sanitization.
3. Human unblocks `fetch`/`git` sign-off items (see Open Decisions).
4. Sec Ops closes remaining blocked items.

### Owner: Sec Ops (audit + fixes); AI Ops (allow-list fix)

### CIL: High if new Critical/High findings emerge; Medium baseline

---

## Open Human Decisions

| # | Question | Blocks |
| --- | --- | --- |
| 1 | Is `mcp>=1.2.0` sufficient for FastMCP, or do you want the standalone `fastmcp` PyPI package? | WS4 |
| 2 | What counts as "human explicitly grants" external documentation access — mention in chat? Specific phrase? Session-level flag? | WS6 `research.prompt.md` fix |
| 3 | Name of the first `plans/` branch folder (matches your branch naming scheme once Dev Ops proposes the convention) | WS3 |
| 4 | `fetch` MCP: confirm per-request human approval via VS Code Configure Tools is the accepted interim control (vs. domain allow-list) | WS8 / Sec Ops blocked item |
| 5 | `git` MCP: run `uvx mcp-server-git --help` or enumerate tools; confirm only read-only tools are approved | WS8 / Sec Ops blocked item |

Existing metrics questions from old `METRICS.md` section are **removed** from agent scope; they are no longer needed given the simplified approach.

---

## Impacted Files

**Modified:**
- `.github/governance/METRICS.md` — stripped to CSS/DIS/CIL only
- `.github/copilot-instructions.md` — add reference to CIL protocols
- `.github/governance/DEVELOPER_GUIDE.md` — update CIL section to reference protocol files
- `mcp-server/server.py` — add `list_active_plans`, `read_plan`; fix `verify_architecture_readonly` allow-list
- `mcp-server/requirements.txt` — confirm or update FastMCP dependency
- `.github/mcp/bitcraft-mcp-spec.md` — document new plan tools
- `.github/prompts/plan.prompt.md` — fix "nearby"
- `.github/prompts/overview.prompt.md` — fix "relevant"
- `.github/prompts/develop.prompt.md` — fix "smallest correct path"
- `.github/prompts/write.prompt.md` — fix "enough repository evidence"
- `.github/prompts/review.prompt.md` — fix "when needed"
- `.github/prompts/metrics.prompt.md` — update to CSS/DIS/CIL only purpose
- `.github/prompts/research.prompt.md` — **pending human decision 2**

**Created:**
- `.github/config/cil-low.yaml`
- `.github/config/cil-medium.yaml`
- `.github/config/cil-high.yaml`
- `.github/config/cil-critical.yaml`
- `.github/plans/README.md`
- `.github/governance/RESEARCH_MCP_VALE_SECURITY.md` (Researcher deliverable)

**Gitignore:**
- No changes. `plans/` and `protocols/` are tracked static-document surfaces. Inboxes are local-only.

---

## CSS, DIS, and CIL for This Batch

- CSS: 5 (touches prompts, governance, MCP server logic, security tooling — not runtime production code)
- DIS: 6 (all governance docs, MCP server, prompt files, instructions, and skill references to metrics)
- CIL: **Medium** (Lead Dev + AI Ops + Sec Ops review before merge; Sec Ops for security-adjacent changes)

---

## Delegation Plan

| Work Stream | Primary | Supporting | Notes |
| --- | --- | --- | --- |
| WS1 — METRICS.md simplification | AI Ops | — | Unblocked. Start immediately. |
| WS2 — CIL protocols | AI Ops | Planner (review) | Unblocked. Deliver 4 YAML files. |
| WS3 — Plans structure + MCP | AI Ops | Dev Ops (review folder design) | Unblocked. Create folders + 2 MCP tools. |
| WS4 — FastMCP confirmation | AI Ops | — | Pending human decision 1. If Option A, no code change. |
| WS5 — Security research | Researcher | Sec Ops (review) | Requires human grant for web access. |
| WS6 — Prompt efficiency fixes | AI Ops | — | Fixable items: unblocked. Pending items: await human decision 2. |
| WS7 — Architecture validation | AI Ops | — | Run after WS1–WS6 complete. |
| WS8 — Security audit + critical fixes | Sec Ops | AI Ops (allow-list fix) | Allow-list fix: unblocked. Audit: after WS5. |
| Agent backlog | AI Ops + Sec Ops | — | See Inbox Updates. |

Lead Dev owns overall coordination and final review routing.

---

## Validation Plan

After each work stream implementation:

1. `npx markdownlint-cli2 --config .markdownlint.jsonc ".github/**/*.md" "*.md"` — no new errors.
2. `vale --config .vale.ini .github docs *.md` — no new failures.
3. MCP smoke test (`python mcp-server/server.py` start + `list_active_plans`, `read_plan` calls).
4. `verify_architecture_readonly` — no false positives for accepted packages after allow-list fix.
5. Prompt review — each updated prompt file re-read against its updated role for internal consistency.

---

## Inbox Updates Queued

After this plan document is written:
- AI Ops inbox: WS1, WS2, WS3, WS6, WS7 items.
- Sec Ops inbox: WS8 audit trigger.
- Researcher inbox: WS5 research trigger.
- Lead Dev inbox: coordination item for this batch.
- Typewriter inbox: post-batch docs update (existing parked item remains).
