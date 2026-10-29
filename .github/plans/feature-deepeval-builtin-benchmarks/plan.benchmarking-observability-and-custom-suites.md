---
created_at: 2026-09-21
edited_at: 2026-09-21
creator: planner
editor: planner
ai_gen: true
status: done
css: 7
dis: 7
cil: high
---
# Sub-Plan: Benchmarking Observability, Custom Suites, and Housekeeping

Origin: Planner, 2026-09-21. Human-reported defects after the first live standard
benchmark run, plus six numbered directives covering progress visibility, run
lifecycle, naming, custom benchmarks, RAG grounding, and code quality.

Supersedes the Arduino preset-case approach. Absorbs review findings F1-F4 from
`.github/reviews/feature-deepeval-builtin-benchmarks/review.benchmarking-ux-consolidation.md`.

## Goal

Make benchmark runs observable end to end, fix the run lifecycle so a run cannot
stall in `finalizing`, rename the history view to `Evaluations`, replace the
hand-rolled Arduino preset cases with a real DeepEval custom benchmark, ground the
RAG metric catalogue in DeepEval's documented behaviour, and add STE-compliant
tooltips and help sections across the Benchmarking workflow.

## Verified Current State (evidence, 2026-09-21)

### Bug 1 — Score 0%, no task list, no progress

- The sidecar reports only coarse stage names. `execute_benchmark` calls
  `stage_writer("loading-dataset")`, `stage_writer("evaluating")`, then
  `stage_writer("finalizing")` (`sidecar/app/tools/deepeval_llm_benchmarks.py:401-407`).
  No counts are emitted. Decision D-R8 accepted this.
- `StandardBenchmarkRun` has no `ProgressPercent`, `CompletedTasks`, or `TotalTasks`
  (`src/Bitcraft.Domain/Benchmarking/StandardBenchmarkRun.cs:35`). Only
  `ProgressStage` and `TaskCount` exist. The frontend therefore cannot show
  completed/total for standard runs.
- **Score 0% root cause (High confidence).** `overall_score` is read with
  `float(getattr(benchmark, "overall_score", 0.0) or 0.0)`
  (`deepeval_llm_benchmarks.py:409`). DeepEval 4.2.3's base result model is
  `DeepEvalBaseBenchmarkResult` with field **`overall_accuracy`**, not
  `overall_score` (`.conda/Lib/site-packages/deepeval/benchmarks/base_benchmark.py:9-11`).
  If the concrete benchmark exposes `overall_accuracy`, `getattr` falls back to
  `0.0` and every run reports 0%. This must be verified against the concrete
  benchmark class before fixing.
- **Task list gap.** The wizard sends `tasks: selectedTasks.length ? selectedTasks : null`.
  The resolved task list is never surfaced before launch, so the user cannot see
  which tasks will run.

### Bug 2 — Run stalls in `finalizing`, next run stays Queued

- **Root cause (High confidence).** `_slot_held()` derives the single runner slot
  from `_last_status_path`, an in-process module global
  (`deepeval_llm_benchmarks.py:322-336`). The runner writes
  `base_status("completed", "finalizing")` as its terminal write
  (`sidecar/app/runner/__main__.py:103`). So `finalizing` is the *terminal* stage
  name, not an in-progress one. The backend's `PollUntilTerminalAsync` only exits
  on `state == "completed"`, which the runner does write — so the backend should
  settle. The observed stall means either the runner died after the `evaluating`
  write, or the backend poller was restarted.
- **Aggravating factor.** `_last_status_path` is process-local. If the sidecar
  container restarts, the slot state is lost while the DB row stays `Running`.
- **Recovery gap.** `StandardBenchmarkRunWorker.RecoverPersistedRunsAsync` requeues
  rows in `Queued` or `Running` at startup
  (`StandardBenchmarkRunWorker.cs:28-37`, repository query at
  `StandardBenchmarkRunRepository.cs:43-48`). But `ProcessQueuedStandardBenchmarkRunHandler`
  calls `StartStandardLlmBenchmarkAsync`, which hits `_slot_held()`. If the slot is
  held by a stale run, the new run fails or blocks. There is no timeout-based
  reclamation of a run stuck in `Running`.
- **Queue starvation.** `InMemoryStandardBenchmarkRunQueue` is a bounded channel
  with `SingleReader = true`, and `ExecuteAsync` awaits each run to completion
  before reading the next (`StandardBenchmarkRunWorker.cs:22-26`). One stalled run
  blocks every later run. This is why the new run stays `Queued`.

### Naming and views

- `BenchmarkingPage.jsx` tab key is `history`, label `Run history`.
- `BenchmarkTemplatesPanel.jsx` is a single flat template manager (create, list,
  delete) with a metric picker driven by `METRIC_DETAILS`
  (`benchmarkingUtils.js:1-100`).
- `ARDUINO_PRESET_CASES` is a hardcoded 3-case JS array
  (`tutor-studio/src/benchmarking/presetBenchmarks.js:1-38`) fed into the generic
  tutor benchmark path. It is not a DeepEval benchmark.

### DeepEval custom benchmark API (verified against installed 4.2.3)

- Installed version: **deepeval 4.2.3**
  (`.conda/Lib/site-packages/deepeval-4.2.3.dist-info`).
- Base class: `DeepEvalBaseBenchmark(ABC, Generic[T])`
  (`deepeval/benchmarks/base_benchmark.py:14`). Constructor takes
  `dataset: Optional["Dataset"] = None`. **A pre-built dataset can be injected.**
- Two abstract methods: `load_benchmark_dataset(*args, **kwargs) -> List[Golden]`
  and `evaluate(model: DeepEvalBaseLLM, *args, **kwargs) -> DeepEvalBaseBenchmarkResult`.
- Result model: `DeepEvalBaseBenchmarkResult(BaseModel)` with
  `overall_accuracy: float` (`base_benchmark.py:9-11`).
- Custom metrics: inherit `BaseMetric`, implement `measure()` and `a_measure()`,
  set `threshold`, `evaluation_model`, `include_reason`, `strict_mode`,
  `async_mode` (https://deepeval.com/docs/metrics-custom).
- Official docs confirm `overall_score`, `task_scores`, and `predictions` are the
  documented result attributes, and that `task_scores` is a **pandas DataFrame**
  (https://deepeval.com/docs/benchmarks-introduction). The sidecar's
  `_normalize_task_scores` expects a dict-like `.items()`
  (`deepeval_llm_benchmarks.py:449-470`) — **shape mismatch, must be verified.**
- **Multimodal: SUPPORTED (corrected 2026-09-21).** An earlier pass reported this
  as unverified. Direct inspection of the installed 4.2.3 tree corrects that:
  - `MLLMImage` dataclass exists (`test_case/llm_test_case.py:41`) with fields
    `dataBase64`, `mimeType`, `url`, `local`, `filename`. It accepts a local path
    or a remote URL and base64-encodes local files.
  - `LLMTestCase` carries a `multimodal: bool` field
    (`test_case/llm_test_case.py:405`).
  - Images embed into the `input` string as `[DEEPEVAL:IMAGE:<id>]`
    placeholders, resolved through `_MLLM_IMAGE_REGISTRY`
    (`test_case/llm_test_case.py:29`, `parse_multimodal_string` at line 148).
  - Multimodal metrics exist under `metrics/multimodal_metrics/`
    (`image_coherence`, `image_editing`, `image_helpfulness`, `image_reference`,
    `text_to_image`). Each inherits `BaseMetric` and implements `measure()` and
    `a_measure()` — e.g. `ImageCoherenceMetric(BaseMetric)` at
    `metrics/multimodal_metrics/image_coherence/image_coherence.py:28`.
  - There is **no** `MLLMTestCase` and **no** `BaseMultimodalMetric` in 4.2.3.
    Multimodal input goes through `LLMTestCase` with `multimodal=True` plus
    embedded `MLLMImage` placeholders. Do not reference the absent classes.
- **Image support is model-dependent.** A text-only model rejects an image
  request. The existing providers already detect this: `OllamaGenerationProvider`
  maps HTTP 400/415/422 on an image request to `Ollama.ImageRejected`
  (`OllamaGenerationProvider.cs:81-84`), and the same pattern exists in the
  OpenAI-compatible and Anthropic providers. `ILlmGenerationProvider` has a
  default `GenerateAsync(LlmGenerationRequest)` overload that fails with
  `Llm.ImageInputNotSupported` when `Images` is non-empty
  (`ILlmGenerationProvider.cs:14-18`). `LlmGenerationRequest` already carries
  `IReadOnlyList<LlmImageInput>? Images`
  (`Common/Models/TutorRuntimeModels.cs:86`).

## Non-Goals

- No change to the tutor-scoped RAG benchmark pipeline's scoring engine.
- No new EF migration unless the progress fields (WS2) require persistence.
- No soft-delete introduction.
- No Git writes. Commit, push, and PR stay human-owned.
- No editing UI for custom benchmarks (see D-2).

## Assumptions

- The sidecar runs as a single container with one runner slot (D-R2). Concurrency
  is out of scope.
- `overall_score` vs `overall_accuracy` is the likely 0% cause but is unconfirmed
  until the concrete benchmark class is inspected at runtime.

## Work Streams

### WS0 — Modality preflight before launch (backend + sidecar)

Phase 2 skill: `bitcraft-backend-feature` (backend), `bitcraft-python-sidecar` (sidecar).

Derived from D-1 and DR-2. A benchmark must not start against a model that cannot
accept the suite's input modality. This avoids the expected-failure class the
human called out.

1. In `src/Bitcraft.Domain/Benchmarking/`, add a `BenchmarkInputModality` enum with
   values `Text` and `Image`.
   Check: `dotnet build bitcraft.slnx` reports 0 errors.
2. Add a `Modality` property to the standard benchmark suite registry entry and
   set `Text` for every existing suite.
   Check: `dotnet build` reports 0 errors.
3. In `src/Bitcraft.Application/Common/Interfaces/ILlmGenerationProvider.cs`, add
   `Task<Result<bool>> ProbeImageInputSupportAsync(string endpoint, string model, string? apiKey, string? providerType, CancellationToken cancellationToken = default)`.
   Check: `dotnet build` reports 0 errors.
4. Give the interface a default implementation returning
   `Result.Success(false)` so existing providers keep compiling.
   Check: `dotnet build` reports 0 errors.
5. In `OllamaGenerationProvider.cs`, implement the probe by sending a one-pixel
   image request and mapping HTTP 400/415/422 to `Result.Success(false)`.
   Check: `dotnet build` reports 0 errors.
6. In `OpenAiCompatibleGenerationProvider.cs`, implement the same probe.
   Check: `dotnet build` reports 0 errors.
7. In `StandardBenchmarkFeatures.cs`, call the probe in
   `CreateStandardBenchmarkRunHandler` when the suite modality is `Image` and fail
   with `StandardBenchmark.ModalityUnsupported` when the probe returns false.
   Check: `dotnet build` reports 0 errors.
8. Surface the modality on `StandardBenchmarkSuiteResponse` so the frontend can
   show it.
   Check: `dotnet build` reports 0 errors.
9. Add a unit test asserting the handler fails when the probe reports no image
   support.
   Check: `dotnet test tests/Bitcraft.UnitTests` passes.

### WS1 — Diagnose and fix the 0% score (sidecar + backend)

Phase 2 skill: `bitcraft-python-sidecar` (sidecar), `bitcraft-backend-feature` (backend).

1. In `sidecar/app/tools/deepeval_llm_benchmarks.py`, add a debug dump after
   `benchmark.evaluate(...)` that logs `dir(benchmark)` and the type of
   `benchmark.overall_score` and `benchmark.task_scores`.
   Check: running one MMLU run with `n_problems=2` prints the attribute names.
2. Read the concrete benchmark class source under
   `.conda/Lib/site-packages/deepeval/benchmarks/` and record whether it exposes
   `overall_score` or `overall_accuracy`.
   Check: the plan records the exact attribute name with a file:line citation.
3. Fix the score read to use the confirmed attribute, with a fallback chain of
   `overall_score` then `overall_accuracy`.
   Check: a 2-problem MMLU run returns a non-zero score when the model answers correctly.
4. Inspect the concrete `task_scores` type and fix `_normalize_task_scores` to
   handle both a pandas DataFrame and a dict.
   Check: the per-task score table renders in the drawer.
5. Add a sidecar pytest that asserts a non-zero score path with a stub model.
   Check: `pytest` in `sidecar/` passes.

### WS2 — Real progress reporting (domain, sidecar, backend, frontend)

Phase 2 skill: `bitcraft-backend-feature` (backend), `bitcraft-python-sidecar` (sidecar),
`bitcraft-frontend-feature` (frontend).

1. In `src/Bitcraft.Domain/Benchmarking/StandardBenchmarkRun.cs`, add
   `CompletedTasks`, `TotalTasks`, and `ProgressPercent` properties.
   Check: `dotnet build bitcraft.slnx` reports 0 errors.
2. Create an EF migration named `StandardBenchmarkProgress`.
   Check: the migration file exists and `dotnet build` reports 0 errors.
3. In `sidecar/app/runner/status.py`, extend `base_status` with `completed_tasks`
   and `total_tasks`.
   Check: `pytest` in `sidecar/` passes.
4. In `sidecar/app/tools/deepeval_llm_benchmarks.py`, emit progress counts from
   the benchmark's `predictions` DataFrame row count when available.
   Check: the status file contains non-null `completed_tasks` during a run.
5. In `src/Bitcraft.Application/Common/Models/BenchmarkingModels.cs`, add
   `CompletedTasks` and `TotalTasks` to `StandardBenchmarkStatus`.
   Check: `dotnet build` reports 0 errors.
6. In `StandardBenchmarkFeatures.cs`, persist the new fields in
   `PollUntilTerminalAsync`.
   Check: `dotnet build` reports 0 errors.
7. In `src/Bitcraft.Api/Contracts/Tutoring/TutorBenchmarkRuns.cs`, add the fields
   to `StandardBenchmarkRunResponse` and `StandardBenchmarkRunProgressResponse`.
   Check: `dotnet build` reports 0 errors.
8. In `src/Bitcraft.Api/Mapping/TutoringMapping.cs`, map the new fields.
   Check: `dotnet build` reports 0 errors.
9. In `tutor-studio/src/benchmarking/benchmarkingUtils.js`, extend
   `progressIndicator` to read `completedTasks` and `totalTasks`.
   Check: `npm run lint` reports only the pre-existing `main.jsx` error.
10. Add a unit test asserting the progress fields round-trip through the handler.
    Check: `dotnet test tests/Bitcraft.UnitTests` passes.

### WS3 — Fix the stalled-run lifecycle (backend + sidecar)

Phase 2 skill: `bitcraft-backend-feature`.

1. In `StandardBenchmarkFeatures.cs`, add a `StalledAfterSeconds` option to
   `StandardBenchmarkProcessingOptions` with a default of 3600.
   Check: `dotnet build` reports 0 errors.
2. In `ProcessQueuedStandardBenchmarkRunHandler`, before starting, mark any run
   whose `LastProgressUtc` is older than `StalledAfterSeconds` as Failed with code
   `StandardBenchmark.Stalled`.
   Check: `dotnet build` reports 0 errors.
3. In `StandardBenchmarkRunWorker.cs`, run each queued run on a bounded parallel
   lane so one stalled run cannot block the queue.
   Check: `dotnet build` reports 0 errors.
4. In `sidecar/app/tools/deepeval_llm_benchmarks.py`, persist `_last_status_path`
   to a file so the slot survives a sidecar restart.
   Check: `pytest` in `sidecar/` passes.
5. Add a unit test for the stall-reclamation branch.
   Check: `dotnet test tests/Bitcraft.UnitTests` passes.

### WS4 — Rename "Run history" to "Evaluations"

Phase 2 skill: `bitcraft-frontend-feature`.

1. In `tutor-studio/src/BenchmarkingPage.jsx`, change the `history` tab label from
   `Run history` to `Evaluations`.
   Check: `npm run build` exits 0.
2. Keep the tab key `history` so existing URLs and `updateHistoryLocation` keep working.
   Check: grep for `"history"` in `BenchmarkingPage.jsx` still matches the key.
3. Update the empty-state text in `BenchmarkRunHistory.jsx` to match the new name.
   Check: `npm run build` exits 0.

### WS5 — Replace Arduino presets with a DeepEval custom benchmark

Phase 2 skill: `bitcraft-python-sidecar` (sidecar), then
`bitcraft-backend-feature` and `bitcraft-frontend-feature`.

**D-1 and D-2 are resolved.** Ship text-only now. Build the image path so the next
iteration needs only data, not code (DR-1). No editing UI (D-2).

1. Create `sidecar/app/tools/custom_benchmarks/base_custom_benchmark.py` with a
   `BitcraftCustomBenchmark(DeepEvalBaseBenchmark)` base class that owns dataset
   loading from an immutable JSON file and the shared `evaluate` skeleton.
   Check: the module imports without error.
2. Give the base class a `case_to_golden` method that maps one dataset record to a
   `Golden`, reading an optional `image` field.
   Check: a record without `image` produces a text-only `Golden`.
3. Give the base class an `image` branch that builds an `MLLMImage` from a local
   path and embeds `[DEEPEVAL:IMAGE:<id>]` into the `input` string when the record
   carries an image.
   Check: a record with `image` produces an `input` containing the placeholder.
4. Create `sidecar/app/tools/custom_benchmarks/arduino_benchmark.py` inheriting
   the base class and setting the benchmark key `arduino`.
   Check: the module imports without error.
5. Ship the dataset as an immutable JSON file inside the sidecar image, not from
   user input.
   Check: `load_benchmark_dataset` returns a non-empty `List[Golden]`.
6. Implement the four weighted sub-scores as `BaseMetric` subclasses with
   `measure()` and `a_measure()`: detail detection 0.20, circuit comprehension
   0.30, solution reasoning 0.30, guidance pedagogy 0.20.
   Check: each metric returns a score and a reason string.
7. Register the benchmark key in `_BENCHMARK_CLASS_NAMES` and the suite registry
   with modality `Text` for this iteration.
   Check: the suite appears in `GET /api/standard-benchmark-runs/suites`.
8. Delete `ARDUINO_PRESET_CASES` from `tutor-studio/src/benchmarking/presetBenchmarks.js`.
   Check: grep `ARDUINO_PRESET_CASES` in `tutor-studio/` returns 0 matches.
9. Remove the Arduino branch from `BenchmarkRunWizard.jsx`.
   Check: `npm run build` exits 0.
10. Add a sidecar pytest covering dataset load, text-only scoring, and the image
    branch with a stub image file.
    Check: `pytest` in `sidecar/` passes.

### WS6 — Templates tab split and custom-benchmark listing

Phase 2 skill: `bitcraft-frontend-feature`.

**D-2 is resolved: read-only. No editor.**

1. In `BenchmarkTemplatesPanel.jsx`, add two sub-tabs: `Metrics & RAG` and
   `Custom benchmarks`.
   Check: `npm run build` exits 0.
2. Keep the existing template manager under `Metrics & RAG` unchanged.
   Check: template create and delete still work.
3. Under `Custom benchmarks`, render a read-only list of loaded custom suites with
   name, description, task count, modality, and enabled state.
   Check: `npm run build` exits 0.
4. Do not add a create, edit, or delete control under `Custom benchmarks`.
   Check: no form or mutation button exists under that tab.

### WS7 — Ground the RAG metric catalogue and add tooltips

Phase 2 skill: `bitcraft-frontend-feature`.

1. For each key in `METRIC_DETAILS` (`benchmarkingUtils.js:1-100`), verify the
   description against DeepEval's documented metric behaviour.
   Check: each entry carries a docs URL or a documented-behaviour citation.
2. Rewrite each `description` in STE: active voice, one clause, no phrasal verbs.
   Check: no description exceeds 20 words.
3. Add a `derivation` field to each entry explaining how the score is computed.
   Check: every entry has a non-empty `derivation`.
4. Group `METRIC_DETAILS` by tier in the template metric picker.
   Check: the picker renders Tier 1 and Tier 2 groups.
5. Create `tutor-studio/src/components/InfoTooltip.jsx` as the reusable tooltip
   component.
   Check: the component accepts `title` and `text` props.
6. Replace ad-hoc `title` attributes across the Benchmarking views with
   `InfoTooltip`.
   Check: `npm run lint` reports only the pre-existing `main.jsx` error.

### WS8 — Help sections across Benchmarking views

Phase 2 skill: `bitcraft-frontend-feature`.

1. Create `tutor-studio/src/benchmarking/BenchmarkingHelp.jsx` exporting a
   reusable help panel component.
   Check: the component renders a title and a list of steps.
2. Add a help panel to the `New run` tab covering revision selection, category
   choice, and launch.
   Check: `npm run build` exits 0.
3. Add a help panel to the `Evaluations` tab covering the queue, polling, and where
   to navigate after a job is created.
   Check: `npm run build` exits 0.
4. Add a help panel to the `Templates` tab covering metric tiers and thresholds.
   Check: `npm run build` exits 0.
5. Write all help copy in STE: active voice, one instruction per sentence, max 20
   words per sentence.
   Check: no sentence contains a semicolon.

### WS9 — Code quality, drift, and housekeeping

Phase 2 skill: `bitcraft-review` (audit), then `bitcraft-frontend-feature` and
`bitcraft-backend-feature` for fixes.

1. Apply review findings F1-F4 from
   `.github/reviews/feature-deepeval-builtin-benchmarks/review.benchmarking-ux-consolidation.md`.
   Check: each finding's file and line no longer exhibits the defect.
2. Grep `tutor-studio/src/` and `src/` for unused exports, dead state, and
   commented-out blocks introduced on this branch.
   Check: each match is either removed or justified in the plan.
3. Verify Clean Architecture direction with the architecture spot check.
   Check: the spot check reports 0 violations.
4. Verify no `src/Bitcraft.Application` project references an external package.
   Check: `Bitcraft.Application.csproj` has no `PackageReference`.
5. Move completed sub-plans to
   `.github/plans/feature-deepeval-builtin-benchmarks/archive/`.
   Check: the active folder holds only `plan.yaml`, `plan.md`, `context.md`, and
   open sub-plans.
6. Move the processed review to `.github/reviews/archive/`.
   Check: the review file is under `archive/`.
7. Prune resolved inbox items across all agent inboxes.
   Check: each inbox holds only open items.

## CSS / DIS / CIL

- **CSS 7** — adds persistence fields (migration), changes run lifecycle and queue
  behaviour, and touches the sidecar execution path.
- **DIS 7** — spans Domain, Application, Infrastructure, Api, sidecar, and frontend.
- **CIL High** — CSS 7 and DIS 7. Protocol: `.github/config/cil-high.yaml`.
  Requires Lead Dev implementation plus Sec Ops review (sidecar subprocess surface
  and queue behaviour) and human validation.

## Impacted Files

- `src/Bitcraft.Domain/Benchmarking/` (WS0 modality enum, WS2 progress fields)
- `src/Bitcraft.Application/Common/Interfaces/ILlmGenerationProvider.cs` (WS0)
- `src/Bitcraft.Infrastructure/ExternalServices/Ollama/OllamaGenerationProvider.cs` (WS0)
- `src/Bitcraft.Infrastructure/ExternalServices/OpenAI/OpenAiCompatibleGenerationProvider.cs` (WS0)
- `src/Bitcraft.Application/Features/Benchmarking/StandardBenchmarkFeatures.cs` (WS0, WS2, WS3)
- `sidecar/app/tools/custom_benchmarks/base_custom_benchmark.py` (WS5, new)
- `sidecar/app/tools/custom_benchmarks/arduino_benchmark.py` (WS5, new)
- `sidecar/app/tools/deepeval_llm_benchmarks.py` (WS1, WS2, WS3, WS5)
- `sidecar/app/runner/status.py`, `sidecar/app/runner/__main__.py` (WS2)
- `sidecar/app/tools/custom_benchmarks/arduino_benchmark.py` (WS5, new)
- `src/Bitcraft.Domain/Benchmarking/StandardBenchmarkRun.cs` (WS2)
- `src/Bitcraft.Application/Common/Models/BenchmarkingModels.cs` (WS2)
- `src/Bitcraft.Application/Features/Benchmarking/StandardBenchmarkFeatures.cs` (WS2, WS3)
- `src/Bitcraft.Infrastructure/ExternalServices/Benchmarking/StandardBenchmarkRunWorker.cs` (WS3)
- `src/Bitcraft.Api/Contracts/Tutoring/TutorBenchmarkRuns.cs` (WS2)
- `src/Bitcraft.Api/Mapping/TutoringMapping.cs` (WS2)
- `tutor-studio/src/BenchmarkingPage.jsx` (WS4, WS8)
- `tutor-studio/src/benchmarking/BenchmarkRunHistory.jsx` (WS2, WS4, WS8)
- `tutor-studio/src/benchmarking/BenchmarkRunWizard.jsx` (WS5, WS7, WS8)
- `tutor-studio/src/benchmarking/BenchmarkTemplatesPanel.jsx` (WS6, WS8)
- `tutor-studio/src/benchmarking/benchmarkingUtils.js` (WS2, WS7)
- `tutor-studio/src/benchmarking/presetBenchmarks.js` (WS5, delete)
- `tutor-studio/src/components/InfoTooltip.jsx` (WS7, new)
- `tutor-studio/src/benchmarking/BenchmarkingHelp.jsx` (WS8, new)

## Delegation Plan

- WS0: Lead Dev with `bitcraft-backend-feature` and `bitcraft-python-sidecar`.
- WS1, WS5: Lead Dev with `bitcraft-python-sidecar`.
- WS2, WS3: Lead Dev with `bitcraft-backend-feature`.
- WS4, WS6, WS7, WS8: Lead Dev with `bitcraft-frontend-feature`.
- WS9: Lead Dev with `bitcraft-review`, then Secretary for housekeeping.
- Sec Ops: review WS3 queue and stall-reclamation changes, and WS5 subprocess surface.
- AI Ops: confirm no agentic-steering conflict from the new skills and instructions.

## Validation Plan

1. `dotnet build bitcraft.slnx` — 0 errors.
2. `dotnet test tests/Bitcraft.UnitTests` — all pass.
3. `dotnet test tests/Bitcraft.ApiIntegrationTests` — all pass.
4. `dotnet test tests/Bitcraft.InfrastructureIntegrationTests` — all pass.
5. `pytest` in `sidecar/` — all pass.
6. `npm run lint` in `tutor-studio/` — only the pre-existing `main.jsx` error.
7. `npm run build` in `tutor-studio/` — exit 0.
8. Manual: launch a 2-problem MMLU run, confirm a non-zero score, per-task scores,
   and a progress indicator that advances.
9. Manual: confirm a second run is not blocked after the first completes.
10. Manual: launch an image-modality suite against a text-only model and confirm
    the run is rejected before it starts with `StandardBenchmark.ModalityUnsupported`.
11. Report validation performed and anything intentionally not run.

## Risks

- **Earlier multimodal finding was wrong.** A first pass reported multimodal as
  unverified. Direct inspection of the installed package corrected it. Recorded
  here so a later reader does not re-litigate it.
- **Image support varies per model.** Mitigation: WS0 probes before launch and
  fails fast with `StandardBenchmark.ModalityUnsupported`.
- **`overall_score` vs `overall_accuracy` is unconfirmed.** Mitigation: WS1 step 2
  verifies before any fix. Do not guess.
- **`task_scores` shape mismatch.** Docs say DataFrame; the sidecar expects a dict.
  Mitigation: WS1 step 4 handles both.
- **Multimodal may be unsupported in deepeval 4.2.3.** Mitigation: WS5 step 1
  confirms before design. If unsupported, fall back to a text-only custom
  benchmark and raise a human decision.
- **WS2 adds a migration.** Mitigation: additive columns only, no data rewrite.
- **WS3 changes queue concurrency.** Mitigation: bound the parallel lane and keep
  the single sidecar slot enforced.

## Human Decisions (resolved 2026-09-21)

- **D-1 — RESOLVED.** Arduino benchmarking is mostly image input plus a problem
  statement. The goal is fault detection and problem resolution testing. **This
  iteration ships text-only, but the image-input path must be built and supported
  now** so the next iteration can use it without rework. Multimodal is confirmed
  available in deepeval 4.2.3 (see Verified Current State).
- **D-2 — RESOLVED: read-only.** No custom-benchmark authoring in the UI. We
  develop benchmarks and include them. Ship a read-only list only. No editor.
- **D-3 — RESOLVED: recommendation accepted.** Rename the tab label to
  `Evaluations`. Keep the URL key `history` so existing links keep working.
- **D-4 — RESOLVED: recommendation accepted.** Add the EF migration if needed.
  Constraints from the human: do not bloat, reuse classes adequately, and use
  base classes and abstraction to keep the code readable.

## Derived Rules (planner, under the resolutions)

- **DR-1 (from D-1):** The custom benchmark dataset schema carries an optional
  image reference per case from day one. Text-only cases leave it null. The
  evaluation path branches on image presence, so enabling images later is a data
  change, not a code change.
- **DR-2 (from D-1):** A run must not start against a model that cannot accept the
  input modality the suite requires. WS0 adds this preflight.
- **DR-3 (from D-4):** Progress fields reuse the existing `BenchmarkRun` naming
  (`CompletedCaseCount`, `TotalCaseCount`, `ProgressPercent`) rather than
  introducing parallel names. The standard-run entity gains the same three
  fields; no new abstraction is created for its own sake.

## Phase 2 Prompt

Run the `bitcraft-develop` skill for
`.github/plans/feature-deepeval-builtin-benchmarks/plan.benchmarking-observability-and-custom-suites.md`.
Execute work steps WS0 through WS9 in order. Validate each step before the next.
All human decisions D-1 through D-4 are resolved. Do not add scope beyond the
named steps.

## Phase 2 Execution Notes (Lead Dev, 2026-09-21)

### Drift — Bug 1 root cause (WS1)

The plan's stated root cause for the 0% score is **disproven by direct
inspection**. Every concrete deepeval 4.2.3 benchmark sets
`self.overall_score` (a 0-1 float): `mmlu.py:160`, `gsm8k.py:88`,
`hellaswag.py:164`, `drop.py:168`, `truthful_qa.py:170`,
`big_bench_hard.py:193`. So `getattr(benchmark, "overall_score", 0.0)` returns
the real value, not 0.0. The score read was never the cause of a 0% display.

The **confirmed** defect is in the result plumbing: `SidecarLlmBenchmarkResponse`
had no `task_scores` field, but the runner reads `result.task_scores`
(`sidecar/app/runner/__main__.py:107`). After `evaluate()` succeeded, that read
raised `AttributeError`, so the terminal status was never written and the run
stalled in `evaluating`. This is the real Bug 2 mechanism (the plan attributed
the stall to the serial worker / process-local slot, which are aggravating
factors but not the primary cause).

Fix applied: added `task_scores` to the response schema and populated it;
`_normalize_task_scores` now handles a pandas DataFrame (`Task`/`Score` columns)
as well as a dict; `_read_overall_score` uses a fallback chain
(`overall_score` → result `overall_accuracy` → result `overall_score` → 0.0).

### WS0 note

No enabled Image suite exists in the registry yet (arduino ships as Text in
WS5), so the modality preflight branch is exercised in unit tests via a
synthetic Image suite rather than through the live registry. The handler was
unsealed and `CheckModalitySupportAsync` / `ProbeImageSupportAsync` are
`protected internal virtual` seams for that test.

### WS5 note — MLLMImage signature

`MLLMImage` takes the path in `url`, not `local`. `local` is a boolean flag
(`llm_test_case.py:41-80`). Passing `local=<path>` raises `ValueError`. The
placeholder id is `image._id`, not `id(image)`.

### Validation performed (2026-09-21)

- `dotnet build bitcraft.slnx` — 0 errors, 0 warnings. PASS.
- `dotnet test tests/Bitcraft.UnitTests` — 417 passed. PASS.
- `dotnet test tests/Bitcraft.ApiIntegrationTests` — 39 passed. PASS.
- `dotnet test tests/Bitcraft.InfrastructureIntegrationTests` — 28 passed. PASS.
- `pytest` in `sidecar/` — 14 passed, 2 skipped (pandas + `datasets` are absent
  from the local conda env; both are installed in the sidecar image). PASS.
- `npm run lint` in `tutor-studio/` — only the pre-existing `main.jsx` error. PASS.
- `npm run build` in `tutor-studio/` — exit 0. PASS.
- Architecture spot check — 2 violations, both pre-existing
  (`Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`
  in `Bitcraft.Application`). `git diff` confirms the csproj is unmodified on
  this branch, so neither was introduced here.

### Validation not run

- Manual UI walk (plan steps 8-10): needs a human in the loop.
- Live benchmark run: needs a real dataset download plus a live model.
- The two skipped sidecar tests: need pandas / `datasets` in the local env.

## Phase 3 Review (Lead Dev, 2026-09-21)

Scope: the full WS0-WS9 working-tree diff (41 files, +1134/-1900).

### R1 (High) — parallel lanes dropped queued runs. FIXED IN PLACE

`StandardBenchmarkRunWorker.MaxParallelLanes` was set to 2 by WS3 step 3. The
sidecar enforces exactly one runner slot (decision D-R2) and returns 409 when a
second run starts. The backend maps 409 to `StandardBenchmark.SidecarBusy` and
marks the run Failed. Two queued runs therefore started together and the loser
was discarded instead of waiting its turn. The change made the queue lossy, which
is worse than the stall it was meant to fix.

Fix: `MaxParallelLanes` set to 1, matching the sidecar slot. Stall protection
still comes from `ReapStalledRunsAsync` and the per-run timeout. The comment now
records why concurrency is wrong here.

### R2 (Medium) — plan.yaml drifted from the archive move. FIXED IN PLACE

`artifacts` listed four sub-plans that WS9 step 5 moved to `archive/`.
`updated_at` and `revision` were stale. Fixed: `revision` v1.13, `updated_at`
2026-09-21, stale artifact entries replaced with a comment naming the eight
archived files, and a v1.13 note recording the drift and the R1 fix.

### R3 (Medium) — inbox not pruned. FIXED IN PLACE

WS9 step 7 was skipped. The lead-dev inbox held about twelve completed items.
Rewritten to open items only. A backup of the old file is at
`$env:TEMP\lead-dev-inbox-backup.md`; completed records are preserved in this
plan and in `plan.yaml` v1.13.

### R4 (Medium) — Sec Ops review is mandatory and not yet done

CIL High. Three surfaces need Sec Ops: the WS0 image probe (a live generation
call to a configured engine using its stored API key), the WS5 subprocess and
`custom_benchmarks` import path, and the WS3 queue and slot-file semantics.
Routed, not fixed. Sec Ops has no inbox file; the items are recorded in the
lead-dev inbox under "routed to Sec Ops".

### R5 (Low) — progress counts never reached the drawer. FIXED IN PLACE

`StandardBenchmarkRunDrawer` rendered `progressStage` but not the new
`CompletedTasks` / `TotalTasks` / `ProgressPercent`. Added a Progress row that
shows `completed/total` and the percentage when a total is known.

### R6 (Low) — uncapped runs never completed their progress bar. FIXED IN PLACE

`_estimate_total_tasks` returns None when no problem cap is set, which is the
default for `arduino` and for any full-dataset run. The terminal status then
reported a completed count with a null total, so the progress bar never
completed. Fixed: when no estimate exists, the total is reconciled with the
completed count after `evaluate()`.

### R7 (Low) — StalledAfterSeconds not discoverable. FIXED IN PLACE

The option defaulted to 3600 but was absent from `appsettings.json`, unlike its
two siblings. Added explicitly.

### R8 (Low) — docs describe the Arduino suite as image input. NOT FIXED

`docs/benchmarking-templates.md:38-44` describes image input plus a 4-stage
pipeline. The suite ships text-only this iteration (D-1). The file is
`ai_gen: false`, so a human must make this edit. Queued in the inbox.

### R9 (Info) — test seams widen the production surface

`CreateStandardBenchmarkRunHandler` was unsealed and gained two `virtual`
methods so unit tests can stub the probe. The optional `ILlmGenerationProvider?`
constructor parameter relies on DI resolving the default value. Both are
accepted: no Image suite is enabled yet, so the seam is the only way to exercise
the branch. Revisit when a real Image suite ships; a fake provider injected
through DI would then be cleaner than a virtual seam.

### Re-validation after fixes

- `dotnet build bitcraft.slnx` — 0 errors, 0 warnings. PASS.
- `dotnet test tests/Bitcraft.UnitTests` — 417 passed. PASS.
- `dotnet test tests/Bitcraft.ApiIntegrationTests` — 39 passed. PASS.
- `dotnet test tests/Bitcraft.InfrastructureIntegrationTests` — 28 passed. PASS.
- `pytest` in `sidecar/` — 14 passed, 2 skipped. PASS.
- `npm run lint` — only the pre-existing `main.jsx` error. PASS.
- `npm run build` — exit 0. PASS.
