---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
---
# Bitcraft Model Routing

Bitcraft uses multiple model families through GitHub Copilot Pro plus a local Ollama setup on 2x RTX 3090. This page is the quick-reference table. Detailed reasoning lives in `.github/skills/bitcraft-model-routing/SKILL.md`.

Model identifiers are intentionally not hard-coded in agent frontmatter. Pick by family, then choose the current best-available model inside that family in the Copilot client.

## Task Categories

Aligned to the categories surfaced in [Copilot's model-comparison reference](https://docs.github.com/en/copilot/reference/ai-models/model-comparison). Treat these as ordering hints, not hard rules.

| Category | Use for | Preferred family |
| --- | --- | --- |
| General-purpose coding | Most backend/frontend tasks, mixed reading + editing, contract work. | OpenAI GPT or Anthropic Claude (Copilot Pro). |
| Fast / lightweight | Short edits, lint fixes, single-file refactors, low-risk renames. | Local Ollama (Qwen Coder family) or Copilot's lightweight tier. |
| Deep reasoning | Multi-file planning, plan critique, security review, audit prose, ambiguity resolution. | Anthropic Claude (extended thinking) or OpenAI reasoning tier. |
| Long-context research | Reading many files, doc cross-reading, synthesis with citations. | Google Gemini (Copilot Pro) when available, otherwise Claude. |
| Visual / multimodal | Screenshots, diagrams, UI review. | Whichever current Copilot model accepts image input (rotates frequently). |
| Privacy-sensitive | Anything containing secrets, customer data, or tutor user content. | Local Ollama, with redaction before any cloud call. |

## Agent-To-Family Routing

| Work type | Preferred family | Reason |
| --- | --- | --- |
| Backend implementation, Clean Architecture, EF Core | Claude or GPT | Strong code synthesis, refactor accuracy, tool use. |
| Frontend implementation (React/Vite) | Claude or GPT | Same as backend; either family is fine. |
| Multi-file planning, risk classification, plan critique | Claude (deep reasoning) | Strong long-context plan reasoning. |
| Research, broad synthesis, doc cross-reading | Gemini when available, otherwise Claude | Long context and web/research workflows. |
| Code review, security review, contract review | Claude or GPT | Conservative critical reading. |
| Drafting non-shipping docs, summaries, inbox writeups | Local Ollama (DeepSeek or Qwen family) | Privacy preserving, cheap, latency tolerable for non-blocking writes. |
| Bounded refactors, mechanical edits, lints | Local Ollama (Qwen Coder family) | Cheap and deterministic enough for narrow tasks. |
| Junior-dev style scaffolding (future role) | Local Ollama (DeepSeek Coder or Qwen Coder family) | Bounded scope, no production access. |
| Security or privacy sensitive review of secrets | Local Ollama (with redaction first) | Never paste secrets to cloud inference. |

## Orchestration Roles

Bitcraft does not run a separate orchestrator service. Two roles share the orchestration burden inside the markdown agent system:

- **Secretary = triage.** Lightweight, fast, cheap. Reads inboxes, summarizes state, routes the user to the right specialist. Suited to fast / general-purpose models. Should not produce multi-page reasoning.
- **Lead Dev = orchestrator.** Takes development responsibility after a plan is approved, delegates implementation slices, and chooses review routing. Suited to general-purpose or deep-reasoning models depending on Change Impact Level.

A specialist agent (NET Dev, Web Dev, Dev Ops, Sec Ops, AI Ops, Typewriter) should not absorb either role.

## Hybrid Cascade Default

When a task allows it, prefer a cascade over a single expensive call:

1. **Cheap first pass.** Use local Ollama or a lightweight cloud model to do triage, draft, or extract.
2. **Strong second pass.** Use Claude or GPT only for the parts that the first pass could not finish reliably (cross-file reasoning, security review, contract design).
3. **Cheap third pass.** Use local Ollama for mechanical cleanup, lint, and formatting.

Skip the cascade for trivial edits (one model is enough) and for security-sensitive review (use the strongest reasoning model directly).

## Constraints

- Do not hard-code exact model identifiers in agent frontmatter; specific names rotate frequently in Copilot.
- Do not route production credentials, customer data, or unredacted secrets through cloud inference. Use local Ollama with redaction first.
- Do not assume a single model family is always best for a role. Re-evaluate quarterly or after a notable provider release.

## Local Setup Notes

- 2x RTX 3090 supports ~30-70B class models in 4-bit quant comfortably; pick the model size that fits sustained context.
- Ollama runs as a local service; integration with Copilot Pro is via the Bring Your Own Key (BYOK) feature when available in the active Copilot client, or via a dedicated MCP/HTTP bridge.
- Local model use is opt-in per agent and should be documented in `.github/skills/bitcraft-model-routing/SKILL.md`.

## Reflection

Update this table when:

- A model family graduates or regresses in the Copilot Pro picker.
- A local Ollama model proves materially better for a bounded task than cloud equivalents.
- A new privacy requirement forces work onto local inference.
- Audit metrics (CSR, ILR) show a routing choice causing recurring rework.
- Copilot's task-category list (model-comparison page) changes shape.

Reflection ownership: AI Ops.

