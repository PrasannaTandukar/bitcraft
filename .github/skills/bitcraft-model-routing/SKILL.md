---
name: bitcraft-model-routing
description: 'Choose GPT, Claude, Gemini, local Ollama DeepSeek/Qwen, or hybrid model routing for Bitcraft development and AI tutor workflows. Use for 2x RTX 3090 planning, cost control, privacy, future Junior Dev tasks, and inference tradeoffs.'
argument-hint: '[task type, privacy level, budget, model family, or hardware constraint]'
---

# Bitcraft Model Routing

Use this skill to choose the right model path for the job.

## When to Use This Skill

- The user asks which model or tool to use.
- The task involves cost, privacy, local GPUs, Ollama, GPT, Claude, Gemini, DeepSeek, Qwen, Codex, OpenCode, or future Junior Dev routing.
- A workflow needs local/cloud split guidance.
- Agent/model metrics need interpretation.

## Routing Rules

- Use GPT or Claude family models for normal repository coding, planning, review, architecture decisions, and ambiguous cross-cutting changes.
- Use Gemini family models for deep research and writing-heavy synthesis when available and policy-appropriate.
- Use local Ollama on the 2x RTX 3090 setup for bulk summarization, embedding experiments, private draft generation, log triage, and repeated low-risk transformations.
- Treat local DeepSeek/Qwen models as future Junior Dev candidates only after metrics show low rework, good validation outcomes, and safe escalation behavior.
- Use external coding CLIs only as comparative tools unless the user explicitly asks to integrate them.

## Evaluation Criteria

- Privacy and data sensitivity.
- Reasoning difficulty.
- Context size and retrieval needs.
- Cost per month and request budget.
- Latency and local GPU utilization.
- Tool integration and validation support.
- CSR, rework, ILR, loop count, and token/cost trends from `.github/agentic-ai/METRICS.md`.

## Gotchas

- Verify current pricing and model availability before committing to monthly estimates.
- Keep production secrets and sensitive data out of cloud prompts unless policy allows it.
- Do not assume a model name is available in Copilot frontmatter; document model class instead.
