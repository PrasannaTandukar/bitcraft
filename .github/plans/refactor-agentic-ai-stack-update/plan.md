---
created_at: 2026-09-17
edited_at: 2026-09-17
creator: ai-ops
editor: ai-ops
ai_gen: true
---
# Plan: Agentic Stack Modernization (Lean, Grounded, Human-In-Loop)

> Origin: AI Ops, 2026-09-17. Human review: pending.

Branch: `refactor/agentic-ai-stack-update` (human-created 2026-09-17, branched out of `feature/deepeval-builtin-benchmarks`). Config: [plan.yaml](plan.yaml).

## Goal

Modernize the `.github` agentic stack — slim always-on context, migrate prompt files to skills, consolidate 18 overlapping skills, split the MCP spec into implemented vs roadmap, and strip stale references — through small, human-approved phases with validation per phase.

## Non-Goals

- No project feature work, no unit test coverage work, no handoffs redesign.
- No Git write operations (branch, commit, push) — all human-owned.
- No new MCP tools or write-capable MCP defaults.
- No restructuring of `docs/` or product code.
- No changes to `ai_gen` flags; no restoring anything from `archive/`.

## Verified Platform Facts (VS Code docs, updated 2026-09-16)

- `.agent.md` under `.github/agents/` remains the correct workspace agent format; the May-era naming ambiguity note in `PROMPT_STACK_MAINTENANCE.md` is resolved.
- Prompt files are **deprecated for Agent Host sessions**; Microsoft provides a prompt-to-skill migration path. Skills now surface as slash commands alongside prompts.
- `argument-hint`, `target: vscode`, `agents` (subagent list), and `user-invocable` are valid current frontmatter fields.
- Tool-list priority: prompt file `tools` override the referenced agent's `tools` — relevant when migrating prompts to skills.

## Diagnostic Findings (from 2026-09-17 sweep)

| # | Finding | Surface |
| --- | --- | --- |
| F-1 | CSR/ILR/DORA orphan references survive the May METRICS.md strip | `governance/README.md`, `MODEL_ROUTING.md`, skills `bitcraft-agentic-metrics`, `bitcraft-change-risk`, `bitcraft-model-routing` |
| F-2 | AI Ops `agents:` frontmatter lists researcher/planner; TOOLING_MATRIX says AI Ops delegates to Secretary/Typewriter/Planner | `agents/ai-ops.agent.md`, `governance/TOOLING_MATRIX.md` |
| F-3 | Stale open ambiguity: agent-file naming (.agent.md vs AGENT-NAME.md) — now resolved by current docs | `governance/PROMPT_STACK_MAINTENANCE.md` |
| F-4 | 10 prompt files duplicate entrypoint functionality that skills now provide | `.github/prompts/` |
| F-5 | 18 skills with heavy overlap in the agentic/work-management cluster | `.github/skills/` |
| F-6 | MCP spec mixes ~10 implemented tools with ~5+ deferred/gated designs | `.github/mcp/bitcraft-mcp-spec.md` |
| F-7 | AI Ops inbox carries 4-month-old items (repo-memory refresh blocked, MCP smoke test, ambiguity sweep) | `.github/inboxes/ai-ops/inbox.md` |
| F-8 | Always-on `copilot-instructions.md` (~120 lines) duplicates content held in governance docs | `.github/copilot-instructions.md` |

## Work Streams (one human checkpoint each)

### WS1 — Drift fixes (CIL: low) — DONE 2026-09-17

- **Approach**: Stripped CSR/ILR/DORA references; aligned AI Ops `agents:` list; replaced the naming ambiguity note with the verified convention; pruned stale AI Ops inbox items.
- **Owner**: ai-ops
- **Acceptance**: No CSR/ILR/DORA references outside archive/; matrix and agent file agree; markdownlint clean on touched files. **Met.**

### WS1b — Agent consolidation 10 → 5 (CIL: high) — DONE 2026-09-17

- **Approach** (human-directed, added after plan approval): deleted Researcher, NET Dev, Web Dev, Dev Ops, Typewriter agents and inboxes; merged live work items into the surviving five inboxes; rewrote ROSTER, TOOLING_MATRIX, DEVELOPER_GUIDE, PROJECT_BRIEF, CIL yamls, root AGENTS.md.
- **Acceptance**: Five agents and five inboxes; no live doc references retired agents except historical notes. **Met.**

### WS2 — Prompt-to-skill migration (CIL: medium) — DONE 2026-09-17

- **Approach**: Converted all 10 `.prompt.md` workflows into skills. `.github/prompts/` deleted (decision 2: strip fully).
- **Owner**: ai-ops
- **Acceptance**: Each former prompt's workflow is reachable as a skill slash command; no duplicate procedure bodies. **Met** — mapping: overview/compact→work-management, research→research, plan→planning, develop→Lead Dev agent + feature skills, review→review, write→doc-maintenance, metrics→change-risk, audit-agentic→agentic-stack, audit-integrity→devops-release, audit-security→security-review.

### WS3 — Skill consolidation 18 → 14 (CIL: medium) — DONE 2026-09-17

- **Approach**: Merged per the target set below; rewrote descriptions with unambiguous trigger conditions; stripped CSR/ILR content during merge.
- **Owner**: ai-ops
- **Acceptance**: Target set present; each skill has a single clear trigger; no orphaned references to deleted skills in agents, instructions, or governance. **Met** (verified by grep sweep).

Executed target set (14 skills — version-upgrade kept standalone instead of folding into devops-release; ste added):

| Skill | Absorbed | Notes |
| --- | --- | --- |
| bitcraft-work-management | agent-inbox, context-management | Overview, triage, inboxes, compaction |
| bitcraft-research | — | Source-hierarchy rules kept |
| bitcraft-change-risk | agentic-metrics | CSS/DIS/CIL + routing reflection; CSR/ILR stripped |
| bitcraft-planning | — | New; fills the Planner skill gap, absorbed plan prompt |
| bitcraft-backend-feature | — | — |
| bitcraft-frontend-feature | — | — |
| bitcraft-test-hardening | — | — |
| bitcraft-security-review | — | Absorbed audit-security prompt + MCP standing findings pointer |
| bitcraft-devops-release | — | Absorbed audit-integrity prompt; version-upgrade stays separate |
| bitcraft-doc-maintenance | — | Absorbed write prompt + provenance + STE-flavored shipping rule |
| bitcraft-agentic-stack | prompt-engineering, mcp-design, model-routing(skill) | Meta-stack skill; model-routing table stays in governance |
| bitcraft-review | — | Absorbed review prompt, CIL-aware routing |
| bitcraft-version-upgrade | — | Kept standalone — distinct lifecycle from releases |
| bitcraft-ste | — | New; default writing style for all agent-facing text |

### WS4 — MCP spec split (CIL: low)

- **Approach**: Spec documents only implemented tools; deferred/gated tools move to a short "Roadmap" section with explicit "not implemented" status. Cross-check against `mcp-server/server.py`.
- **Owner**: ai-ops
- **Acceptance**: Every tool in the spec's implemented section exists in `server.py`; roadmap items are clearly marked non-functional.

### WS5 — Slim always-on context + STE review sweep (CIL: medium)

- **Approach**: Reduce `copilot-instructions.md` to durable rules only; move procedure-level content into skills (on-demand) and keep pointers to governance. Dedupe against `DEVELOPER_GUIDE.md`. Then STE-review the surviving steering surface: `copilot-instructions.md`, all 6 instructions files, all 5 agent bodies, ROSTER, and the core governance docs (`DEVELOPER_GUIDE`, `METRICS`, `TOOLING_MATRIX`) using the `bitcraft-ste` skill — Strict mode for agent-executed files, STE-flavored for narrative docs. Fix ambiguity (vague verbs, hedge stacks, synonym rotation, run-ons) in place.
- **Owner**: ai-ops
- **Acceptance**: Measurably smaller always-on file; no rule lost — only relocated; steering docs pass an STE scan (no semicolons, no multi-instruction sentences in rule lists, consistent agent names); internal links verified.

### WS6 — Final agentic audit (CIL: low)

- **Approach**: Run `audit-agentic` equivalent against the new stack; write report to `.github/audits/`; convert findings to inbox items.
- **Owner**: ai-ops
- **Acceptance**: Audit report filed; zero unresolved high-severity findings.

## Human Decisions (resolved 2026-09-17)

| # | Question | Decision |
| --- | --- | --- |
| 1 | WS3 target skill set | Approved. Skills must cover all existing prompt workflows with no regressions; each former prompt maps to a skill in WS2/WS3. |
| 2 | Delete `.github/prompts/` after WS2 | Strip stale content fully. No README pointer kept; prompts are deleted once their workflows live in skills. |
| 3 | Public awesome-copilot skills | Copy + adapt ideas; ground consolidated skills against public patterns where relevant. |
| 4 | Agent roster | Approved 2026-09-17: consolidate 10 → 5 agents (Secretary, Planner, Lead Dev, Sec Ops, AI Ops). Executed in this batch. |
| 5 | STE writing standard | Approved 2026-09-17: `bitcraft-ste` skill added (adapted from danyuchn/asd-ste100-skill, MIT); STE is the default writing style for all agent-facing text; steering docs get an STE review sweep in WS5. |

## Impacted Files

- Modified: `copilot-instructions.md`, `governance/README.md`, `governance/MODEL_ROUTING.md`, `governance/PROMPT_STACK_MAINTENANCE.md`, `governance/TOOLING_MATRIX.md`, `agents/ai-ops.agent.md`, `mcp/bitcraft-mcp-spec.md`, local inbox content during execution
- Deleted (WS2, after approval): `.github/prompts/*.prompt.md`
- Created (WS2/WS3): consolidated `skills/*/SKILL.md` set
- Untouched: `src/`, `tests/`, `tutor-studio/`, `docs/`

## CSS, DIS, CIL

- CSS: 7 (repository-wide workflow conventions; wrong changes degrade all future agent sessions)
- DIS: 8 (cross-cutting `.github` surface)
- CIL: high — human checkpoint per work stream; Sec Ops consulted if MCP security findings are touched in WS4.

## Validation Plan

- Per phase: markdownlint + Vale on touched Markdown (per `PROMPT_STACK_MAINTENANCE.md` static checks).
- After WS2/WS3: VS Code Chat Diagnostics view to confirm all agents/skills load without errors (human runs this — requires the client UI).
- After WS4: confirm spec matches `mcp-server/server.py` tool list.
- No backend build/test impact; intentionally not run: `dotnet build`, unit tests.

## Local Inbox Updates Queued

- Use local inboxes only for short-lived coordination.
- Record durable outcomes in this tracked plan or another tracked static document.
