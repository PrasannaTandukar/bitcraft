---
created_at: 2026-08-20
edited_at: 2026-09-19
creator: planner
editor: lead-dev
ai_gen: true
---
# Human Decision Register: DeepEval Built-in Benchmarks

| ID | Topic | Decision | Status | Rationale |
| :--- | :--- | :--- | :--- | :--- |
| **D42** | Standard Benchmark Suite Selection | Focus on zero-sandbox benchmarks: **MMLU, GSM8K, BBH, HellaSwag, DROP, TruthfulQA**. HumanEval deferred. | **LOCKED** | Human confirmed: avoids security sandbox complexity in current iteration. |
| **D43** | Metric Transparency & Separation | Partition metrics into **Tier 1 (Deterministic / Technical)** and **Tier 2 (Qualitative / LLM-as-a-Judge)** across all UI and API responses. | **LOCKED** | Ensures 100% auditable and reproducible metrics while offering optional semantic evaluations. |
| **D44** | Wizard Architecture | Adopt **Option A (Unified Multi-Mode Wizard)** with mode selector (Tutor RAG vs. Standard LLM Benchmark vs. Custom Arduino). | **LOCKED** | Preserves existing views and URLs while scaling to multiple benchmark workflows. |
| **D45** | Continuous Model Eval vs. Systematic Runs | Model evaluation baselines live in `/models` view; systematic regression/task runs live in `/benchmarks` with revision linkages. | **LOCKED** | Separation of ongoing model health vs. discrete tutor revision benchmarks. |
| **D46** | Arduino Benchmark Dimensions | 4-stage scoring pipeline: 1. Detail/Object Detection (0.20), 2. Circuit Understanding (0.30), 3. Solution Reasoning (0.30), 4. Guidance Quality (0.20). | **LOCKED** | Establishes balanced rubric for multimodal STEM tutor evaluation. |

---

## Outstanding Human Decisions for Handover

| ID | Decision Question | Options | Recommended |
| :--- | :--- | :--- | :--- |
| **O-1** | Standard Benchmark Dataset Caching | **RESOLVED 2026-09-19**: named Docker volume for the DeepEval cache in the `benchmark-sidecar` service. Datasets survive container rebuilds. The compose edit routes through Sec Ops review. | Named cache volume (accepted). |
| **O-2** | Default Judge Model Provider | **RESOLVED 2026-09-19**: the judge defaults to the engine under test (self-judging). The earlier `.env`-key recommendation is struck; it contradicted consolidation OD-4 (env/config fallbacks removed). The Tier-2 judge tool stays unregistered until that work resumes. | Engine under test (accepted). |
