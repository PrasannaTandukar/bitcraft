# Bitcraft Model Routing

Bitcraft uses multiple model families through GitHub Copilot Pro plus a local Ollama setup on 2x RTX 3090. This page is the quick-reference table. Detailed reasoning lives in `.github/skills/bitcraft-model-routing/SKILL.md`.

Model identifiers are intentionally not hard-coded in agent frontmatter. Pick by family, then choose the current best-available model inside that family in the Copilot client.

## Routing Table

| Work type | Preferred family | Reason |
| --- | --- | --- |
| Backend implementation, Clean Architecture, EF Core | Anthropic Claude or OpenAI GPT (Copilot Pro) | Strong code synthesis, refactor accuracy, tool use. |
| Frontend implementation (React/Vite) | Anthropic Claude or OpenAI GPT (Copilot Pro) | Same as backend; either family is fine. |
| Multi-file planning, risk classification, plan critique | Anthropic Claude (Copilot Pro) | Strong long-context plan reasoning. |
| Research, broad synthesis, doc cross-reading | Google Gemini (Copilot Pro) when available, otherwise Claude | Long context and web/research workflows. |
| Code review, security review, contract review | Claude or GPT (Copilot Pro) | Conservative critical reading. |
| Drafting non-shipping docs, summaries, inbox writeups | Local Ollama (DeepSeek or Qwen family) | Privacy preserving, cheap, latency tolerable for non-blocking writes. |
| Bounded refactors, mechanical edits, lints | Local Ollama (Qwen Coder family) | Cheap and deterministic enough for narrow tasks. |
| Junior-dev style scaffolding (future role) | Local Ollama (DeepSeek Coder or Qwen Coder family) | Bounded scope, no production access. |
| Security or privacy sensitive review of secrets | Cloud frontier with no training data retention setting, or local Ollama | Never paste secrets; prefer redaction first. |

## Constraints

- Do not hard-code exact model identifiers in agent frontmatter; specific names rotate frequently in Copilot.
- Do not route production credentials, customer data, or unredacted secrets through cloud inference. Use local Ollama with redaction first.
- Do not assume a single model family is always best for a role. Re-evaluate on a quarterly basis or after a notable provider release.

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

Reflection ownership: AI Ops.
