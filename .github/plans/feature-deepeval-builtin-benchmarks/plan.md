---
created_at: 2026-08-20
edited_at: 2026-08-20
creator: planner
editor: lead-dev
ai_gen: true
---
# Implementation Plan: Built-in DeepEval Benchmarks & Model Evaluation Architecture

**Branch:** `feature/deepeval-builtin-benchmarks`  
**CIL:** High | **CSS:** 7 | **DIS:** 7  
**Owner:** Lead Dev (orchestration) $\rightarrow$ NET Dev, Web Dev, Dev Ops, Sec Ops  

---

## 1. Executive Summary & Core Objectives

This plan delivers the DeepEval built-in benchmarking and transparent evaluation layer to Tutor Studio:
1. **Built-in Standardized Benchmarks:** Zero-sandbox execution for **MMLU, GSM8K, BIG-Bench Hard (BBH), HellaSwag, DROP, and TruthfulQA**. (*HumanEval is deferred/excluded due to code execution sandbox constraints*).
2. **Architectural Separation:** Clean separation of **Continuous Model Evaluation & Observability** (latency, throughput, deterministic claims, historical drift) from **Systematic Tutor Task Benchmarking** (suite runs, revision snapshots, regression checks).
3. **Transparent & Deterministic Scoring:** Priority given to fast, reproducible, 100% explainable deterministic metrics (claim overlap, token statistics, latency, PII regex, keyword guards) presented as the primary baseline, clearly distinguished from probabilistic **LLM-as-a-Judge / G-Eval** qualitative layers.
4. **Arduino & Multimodal Benchmark Skeleton:** Extensible schema and evaluation pipeline for visual circuit diagnostic tasks (Image + Code/Prompt) broken into 4 distinct scoring dimensions.
5. **Academic & Safety Grounding:** Empirical literature and safety guardrail frameworks for AI tutors serving young learners/minors.

---

## 2. Benchmark & Metric Classification

### 2.1 Standardized LLM Benchmarks (Direct Model Reasoning)
*Executed via sidecar `deepeval_llm_benchmarks.py` $\to$ Backend `IBenchmarkEngine`.*

| Benchmark | Target Capability | Evaluation Method | Sandbox Required? | Status |
| :--- | :--- | :--- | :--- | :--- |
| **MMLU** | General Knowledge & Multitask Understanding | Multiple-choice accuracy (A/B/C/D) | No | **Active (Target)** |
| **GSM8K** | Grade School Math & Chain-of-Thought | Final numeric regex extraction (`#### <num>`) | No | **Active (Target)** |
| **BIG-Bench Hard** | Complex multi-step algorithmic reasoning | Exact label match | No | **Active (Target)** |
| **HellaSwag** | Commonsense reasoning & sentence completion | Choice prediction / exact match | No | **Active (Target)** |
| **DROP** | Discrete reasoning over paragraphs (dates/counts) | Exact Match & F1 bag-of-words score | No | **Active (Target)** |
| **TruthfulQA** | Truthfulness, myth avoidance, hallucination resistance | Multi-choice accuracy | No | **Active (Target)** |
| **HumanEval** | Python docstring-to-code synthesis | Unit test runtime `pass@k` | **Yes (exec)** | **Deferred (Post-PoC)** |

---

### 2.2 Metric Hierarchy: Deterministic Baseline vs. Probabilistic LLM-Judge

To guarantee auditable observability, all scoring surfaces partition metrics into two distinct visual tiers:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ 🟢 TIER 1: DETERMINISTIC METRICS (Primary & Always Active)                              │
│ • Latency (ms) & Turnaround Breakdown (Decomposition vs Verification)                  │
│ • Token Throughput (tokens/sec) & Token Counts (Prompt / Completion)                   │
│ • Claim Decomposition Grounding Ratio (Context Overlap & Entity Coverage)              │
│ • Exact / F1 / BLEU / ROUGE Token Overlap (Golden Answer comparison)                  │
│ • Deterministic Safety Guards (Regex PII patterns, Forbidden Keyword / Bias Lexicons) │
│ • Tool / JSON Schema Conformance (Strict schema parsing & argument typing)             │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ 🟣 TIER 2: QUALITATIVE & LLM-AS-A-JUDGE (Secondary / Probabilistic Overlay)            │
│ • Step-by-Step Faithfulness (Judge model deduces logical entailment)                  │
│ • Contextual Precision & Recall (Ranked chunk relevance judgment)                      │
│ • Pedagogical G-Eval (Rubrics for tone, hint-giving style, minor safety)               │
│ • Persona & Role Adherence (Judge validates tutor character boundary)                  │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Workflow & UI Architecture (Benchmarking Wizard)

### Unified Multi-Mode Wizard (`BenchmarkRunWizard.jsx`)
The wizard adapts its steps based on the **Run Mode**:

```
                               ┌─────────────────────────────┐
                               │     Select Run Mode         │
                               └──────────────┬──────────────┘
                                              │
               ┌──────────────────────────────┼──────────────────────────────┐
               ▼                              ▼                              ▼
      [Tutor Evaluation]           [Standard Benchmark]             [Custom / Arduino]
   Step 1: Tutor & Revision       Step 1: Model Selection         Step 1: Tutor / Model Target
   Step 2: Template & Metrics     Step 2: Benchmark & Tasks       Step 2: Dataset / Image Suite
   Step 3: Test Cases (Manual/CSV)Step 3: Shot & Parameters       Step 3: Rubric Thresholds
   Step 4: Launch & Trace         Step 4: Launch & Monitor        Step 4: Launch & Multi-score
```

### Separation of Concerns in UI
1. **Model Catalog / Detail View (`/models`):** Shows persistent **Model Evaluation Baseline** (throughput, raw latency, baseline MMLU/GSM8K scores, historical score trend).
2. **Benchmarking Page (`/benchmarks`):** Manages **Systematic Runs** tied to specific Tutor Revisions (`TutorRevisionId`), test suites, deterministic scorecards, and audit logs.

---

## 4. Arduino Custom Evaluation Framework

### 4.1 Four-Dimension Scoring Rubric
For visual circuit & programming problems (e.g., Arduino Uno schematic + LED blinking failure prompt):

```
                                  Total Case Score (1.0)
                                            │
        ┌───────────────────┬───────────────┴───────────────┬───────────────────┐
        ▼                   ▼                               ▼                   ▼
1. Detail Detection  2. Circuit Comprehension       3. Problem Solution   4. Guidance Quality
     (Weight: 0.20)         (Weight: 0.30)            (Weight: 0.30)        (Weight: 0.20)
  Identifies board,    Traces connections, pins       Identifies missing    Pedagogical hint,
 components, ICs, &   (e.g., Pin 13, GND, Resistor)  resistor / floating   avoids direct leak,
 wiring coordinates.   and logic states.             pin / wrong baud.     encourages debugging.
```

### 4.2 Extensible Case Schema (`schemas.py` / C# DTOs)
```json
{
  "case_id": "ard-001",
  "category": "visual_hardware_diagnostic",
  "input_prompt": "My LED on pin 13 is not turning on. Here is my setup and code.",
  "image_url": "data:image/png;base64,...",
  "source_code": "void setup() { pinMode(13, OUTPUT); } void loop() { digitalWrite(13, HIGH); }",
  "goldens": {
    "detected_components": ["Arduino Uno", "Red LED", "220 Ohm Resistor", "Breadboard"],
    "circuit_flaw": "LED cathode is connected to 5V instead of GND",
    "expected_guidance_concept": "Check polarity and ground path without giving away direct answer immediately"
  },
  "rubric_weights": {
    "detail_detection": 0.20,
    "circuit_understanding": 0.30,
    "solution_reasoning": 0.30,
    "guidance_pedagogy": 0.20
  }
}
```

---

## 5. Academic Literature & Safety Grounding for Minor-Safe AI Tutors

When deploying AI agents for young learners/students, evaluation must align with peer-reviewed frameworks for educational reliability and child safety:

1. **Pedagogical Scaffolding & Non-Answer-Leaking (Socratic Evaluation):**
   - *Reference:* Macina et al. (2023), *"Opportunities and Challenges in Neural Dialog Tutoring."* (Findings: evaluating whether LLM tutors scaffold problem solving via incremental hints rather than generating raw answers).
   - *Metric Implementation:* G-Eval rubric penalizing direct code/solution emission when hint requests are made.
2. **Child Safety, Data Privacy & Minor Protection:**
   - *Standards:* COPPA / GDPR-K compliant guardrails, NIST SP 800-162 ABAC.
   - *Metric Implementation:* Deterministic PII scrubbers + strict zero-tolerance toxicity/misuse classifiers.
3. **Hallucination in STEM / Hardware Contexts:**
   - *Reference:* Ji et al. (2023), *"Survey of Hallucination in Natural Language Generation."*
   - *Metric Implementation:* Grounded verification of microcontroller pinouts and register specs against official datasheets.

---

## 6. Implementation Slices & Agent Delegation

```
                                  Lead Dev (Orchestrator)
                                            │
        ┌───────────────────┬───────────────┴───────────────┬───────────────────┐
        ▼                   ▼                               ▼                   ▼
     NET Dev             Web Dev                         Dev Ops             Sec Ops
 (Backend & API)     (Tutor Studio UI)               (Sidecar & Pkg)     (ABAC & Safety)
  • Standard bench    • Multi-mode wizard             • deepeval pinned   • PII regex rules
    MediatR CQRS      • Deterministic/Judge split UI    container build   • Safe error guards
  • DeepEval client   • Arduino test case visualizer  • Ollama bridge     • Model permission
```

### Delegation Tasks
- **Dev Ops:** Ensure `sidecar/requirements.txt` has `deepeval` pinned; enable `/internal/tools/llm-benchmark` routing; verify Ollama container networking.
- **NET Dev:**
  - Update `BenchmarkFeatures.cs` and `DeepEvalBenchmarkContracts.cs` to handle Standard Benchmarks and extended metrics.
  - Implement `StandardLlmBenchmarkCommand` and extend `IBenchmarkEngine` to support standard benchmark suites.
  - Add Arduino multimodal case data contract definitions.
- **Web Dev:**
  - Refactor `BenchmarkRunWizard.jsx` to Option A (Multi-mode selector: Tutor RAG vs. Standard Benchmark vs. Custom Arduino).
  - Update `BenchmarkRunDetails.jsx` to render **Tier 1 (Deterministic)** and **Tier 2 (Qualitative/Judge)** scorecards clearly.
  - Update Models page to display baseline evaluation statistics.
- **Sec Ops:**
  - Audit PII regex patterns and ensure HumanEval remains disabled/locked.
  - Validate prompt injection defenses on judge prompts.
- **Typewriter:**
  - Update `docs/benchmarking-templates.md` with new standard benchmark options, deterministic metric definitions, and Arduino rubric guidelines.

---

## 7. Validation & Acceptance Criteria

1. **Backend Tests:** `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj` passes with new benchmark contracts and mapping tests.
2. **Sidecar Integration:** `pytest tests/Bitcraft.SidecarIntegration/test_sidecar_api.py` verifies `/internal/tools/execute` and `/internal/tools/llm-benchmark` with mock judge calls.
3. **Frontend Validation:** `npm run lint` and `npm run build` in `tutor-studio/` pass without errors.
4. **UI Verification:** All 6 standard benchmarks can be selected and launched; deterministic metrics appear with detailed breakdown cards; Arduino skeleton accepts image + code inputs.
