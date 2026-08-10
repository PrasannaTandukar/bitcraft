---
created_at: 2026-05-19
edited_at: 2026-08-06
creator: human
editor: codex
ai_gen: false
---
# Benchmarking Templates

Bitcraft benchmark templates are editor-managed evaluation profiles that describe how collected tutor outputs should be evaluated by the benchmark sidecar. They are created explicitly so benchmark runs have clear, auditable scoring configuration instead of hidden data created by a read operation.

## Single-turn LLM Evaluation

`single-turn-llm-eval-v1` is the recommended starter profile for evaluating one prompt and one model answer at a time. It is aligned with the DeepEval-style checks used by Bitcraft benchmarking:

- `faithfulness`: checks whether answer claims are grounded in the provided retrieval context.
- `answer_relevancy`: checks whether the answer responds to the input prompt.
- `hallucination`: checks whether answer claims are grounded by context or golden output.
- `correctness`: optional golden-answer comparison when an expected output is available.

## Case Inputs

Each case should include:

- `input_text`: the prompt or user message.
- `actual_output`: the tutor/model answer being evaluated. It may be omitted when automatic generation is explicitly requested; Bitcraft then runs the selected tutor revision before evaluation.
- `context_text`: retrieval context used by the tutor, when available.
- `expected_output`: golden answer, when available.
- `metadata_json`: optional structured labels such as difficulty, scenario, or source.

## Progress

Benchmark runs are asynchronous. The API returns a `runId` immediately after a run is queued. The web app polls the run progress endpoint with that ID until the status becomes `Completed` or `Failed`. Automatic runs progress through queued, generating, and evaluating stages; existing-output runs skip generation. Generated answers and their exact context are persisted before sidecar evaluation.

From the D25 step 8 landing, each run optionally carries a `TutorRevisionId` linking it to the exact revision snapshot that was active when the run was triggered. This enables audit trails across revision history.

## Scoring Engine

The current `deepeval-deterministic` sidecar is a deterministic local scorer based on token and claim overlap. Its results are approximate indicators, not an LLM-as-a-judge or an academic-grade correctness or hallucination assessment. `guidancePath` remains compatibility metadata and is not loaded or applied by the current scorer.
