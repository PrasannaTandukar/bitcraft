---
created_at: 2026-05-19
edited_at: 2026-08-20
creator: human
editor: typewriter
ai_gen: false
---
# Benchmarking Templates & Built-in Suites

Bitcraft benchmark templates and standardized suites provide transparent, reproducible evaluation of both raw model capabilities and tutor RAG configurations.

## Evaluation Metric Tiers

To preserve explainability, all evaluation metrics are separated into two tiers:

### Tier 1: Deterministic & Technical Metrics (Default Baseline)
1. **Faithfulness / Grounding Ratio:** Calculates claim token overlap against retrieved context.
2. **Answer Relevancy:** Prompt-to-response token overlap and semantic coverage.
3. **Hallucination / Groundedness:** Proportion of output claims supported by context or golden answers.
4. **Contextual Precision & Recall:** Signal-to-noise ratio in retrieved knowledge chunks.
5. **PII & Student Privacy Guard:** Deterministic regex scans detecting email, phone numbers, and credential leaks (COPPA/GDPR-K).
6. **Technical Telemetry:** End-to-end latency (ms), decomposition vs. verification breakdown, token throughput (tokens/sec), and estimated compute impact.

### Tier 2: Qualitative & LLM-as-a-Judge Overlays
1. **G-Eval Rubrics:** Weighted criteria using Chain-of-Thought scoring for pedagogical tone and explanation quality.
2. **Step-by-Step Entailment:** LLM judge evaluation of logical consistency.
3. **Role & Persona Adherence:** Ensuring tutor remains inside defined pedagogical boundaries.

## Standardized Zero-Sandbox Benchmarks
Bitcraft supports direct model evaluation against standard research datasets:
- **MMLU:** 57 multi-discipline subjects testing general knowledge.
- **GSM8K:** Grade school multi-step math problems testing numerical chain-of-thought.
- **BIG-Bench Hard (BBH):** 23 algorithmic and multi-step reasoning tasks.
- **HellaSwag:** Adversarial commonsense sentence completion.
- **DROP:** Reading comprehension with discrete numerical reasoning.
- **TruthfulQA:** Testing hallucination resistance and myth avoidance.

## Custom Arduino Visual Diagnostic Skeleton
A specialized multimodal hardware evaluation workflow:
- **Input:** Circuit board / schematic image + diagnostic prompt + C++ source code.
- **4-Stage Scoring Pipeline:**
  1. *Detail & Pinout Detection (20%):* Board, IC, pin, and component identification.
  2. *Circuit Comprehension (30%):* Wiring continuity, polarity, logic levels, and register config.
  3. *Problem Solution & Root Cause (30%):* Diagnosing floating pins, wrong resistors, baud mismatch.
  4. *Pedagogical Guidance Quality (20%):* Socratic hint progression without direct answer leaking.
