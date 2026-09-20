import { PlusOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Col, Descriptions, Empty, Flex, Form, InputNumber, Radio, Row, Select, Space, Spin, Steps, Tag, Typography } from "antd";
import { useEffect, useMemo, useState } from "react";
import { useSelector } from "react-redux";
import { getApiErrorDescription } from "../api/client.js";
import BenchmarkCaseEditor from "./BenchmarkCaseEditor.jsx";
import { createBenchmarkRun, createStandardBenchmarkRun, listBenchmarkTemplates, listInteractionModelsForAdmin, listStandardBenchmarkSuites, listTutorRevisions } from "./benchmarkingApi.js";
import { emptyBenchmarkCase, formatPercent, isUsableRevision, METRIC_DETAILS, metricName, parseMetrics, revisionStatus } from "./benchmarkingUtils.js";
import { ARDUINO_PRESET_CASES } from "./presetBenchmarks.js";
import { STANDARD_BENCHMARK_TASKS } from "./standardBenchmarkTasks.js";

const STEP_LABELS = ["Tutor Selection", "Benchmark Selection", "Review & Launch"];

export default function BenchmarkRunWizard({ tutors, tutorsLoading, tutorsError, initialTutorId, initialRevisionId, onSelectionChange, onConfigureTutor, onOpenTemplates, onCreated }) {
  const [benchmarkType, setBenchmarkType] = useState("rag"); // "rag" | "standard" | "arduino"
  const [step, setStep] = useState(1);
  const [tutorId, setTutorId] = useState(initialTutorId || "");
  const [revisions, setRevisions] = useState([]);
  const [revisionId, setRevisionId] = useState("");
  const [revisionsLoading, setRevisionsLoading] = useState(false);
  const [revisionsError, setRevisionsError] = useState("");
  const [templates, setTemplates] = useState([]);
  const [templateId, setTemplateId] = useState("");
  const [templatesLoading, setTemplatesLoading] = useState(true);
  const [templatesError, setTemplatesError] = useState("");
  const [automatic, setAutomatic] = useState(true);
  const [cases, setCases] = useState([emptyBenchmarkCase()]);
  const [topK, setTopK] = useState(5);
  const [consistencyRuns, setConsistencyRuns] = useState(1);
  const [selectedStandardBenchmark, setSelectedStandardBenchmark] = useState("mmlu");
  const [selectedTasks, setSelectedTasks] = useState([]);
  const [nShots, setNShots] = useState(0);
  const [nProblems, setNProblems] = useState(10);
  const [standardModelId, setStandardModelId] = useState("");
  const [standardSuites, setStandardSuites] = useState([]);
  const [standardSuitesLoading, setStandardSuitesLoading] = useState(true);
  const [standardSuitesError, setStandardSuitesError] = useState("");
  const [interactionModels, setInteractionModels] = useState([]);
  const [interactionModelsLoading, setInteractionModelsLoading] = useState(true);
  const [interactionModelsError, setInteractionModelsError] = useState("");
  const [validationError, setValidationError] = useState("");
  const [launchError, setLaunchError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const { roles = [] } = useSelector((state) => state.auth);
  const isAdmin = roles.includes("Admin");

  const selectedTutor = tutors.find((tutor) => tutor.id === tutorId);
  const selectedRevision = revisions.find((revision) => revision.id === revisionId);
  const selectedTemplate = templates.find((template) => template.id === templateId);
  const selectedMetrics = useMemo(() => parseMetrics(selectedTemplate?.metricsJson), [selectedTemplate]);
  const totalOutputs = automatic ? cases.length * consistencyRuns : cases.length;

  useEffect(() => {
    const controller = new AbortController();
    listBenchmarkTemplates(controller.signal)
      .then((data) => {
        const enabled = (data ?? []).filter((template) => template.isEnabled);
        setTemplates(enabled);
        setTemplateId((current) => enabled.some((template) => template.id === current) ? current : "");
        setTemplatesError("");
      })
      .catch((error) => {
        if (error.name !== "AbortError") setTemplatesError(getApiErrorDescription(error, "Could not load benchmark templates."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setTemplatesLoading(false);
      });
    return () => controller.abort();
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    listStandardBenchmarkSuites(controller.signal)
      .then((data) => {
        setStandardSuites(data ?? []);
        setStandardSuitesError("");
      })
      .catch((error) => {
        if (error.name !== "AbortError") setStandardSuitesError(getApiErrorDescription(error, "Could not load standardized benchmark suites."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setStandardSuitesLoading(false);
      });
    return () => controller.abort();
  }, []);

  // The interaction-model list is Admin-only on the backend. Fetch it only for
  // admins so Editors do not trigger a guaranteed 403 on page load.
  useEffect(() => {
    if (!isAdmin) return undefined;
    const controller = new AbortController();
    listInteractionModelsForAdmin(controller.signal)
      .then((data) => {
        setInteractionModels(data ?? []);
        setInteractionModelsError("");
      })
      .catch((error) => {
        if (error.name !== "AbortError") setInteractionModelsError(getApiErrorDescription(error, "Could not load interaction models."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setInteractionModelsLoading(false);
      });
    return () => controller.abort();
  }, [isAdmin]);

  const selectedStandardSuite = standardSuites.find((suite) => suite.key === selectedStandardBenchmark);
  const selectedStandardModel = interactionModels.find((model) => model.id === standardModelId);

  useEffect(() => {
    if (!tutorId) return undefined;
    const controller = new AbortController();
    listTutorRevisions(tutorId, controller.signal)
      .then((data) => {
        const loaded = data ?? [];
        setRevisions(loaded);
        const requested = loaded.find((revision) => revision.id === initialRevisionId);
        const active = loaded.find((revision) => revisionStatus(revision.status) === "Active");
        const nextRevision = requested || active;
        if (nextRevision) {
          setRevisionId(nextRevision.id);
        }
      })
      .catch((error) => {
        if (error.name !== "AbortError") setRevisionsError(getApiErrorDescription(error, "Could not load tutor revisions."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setRevisionsLoading(false);
      });
    return () => controller.abort();
  }, [initialRevisionId, tutorId]);

  const selectTutor = (value) => {
    setTutorId(value);
    setRevisionId("");
    setRevisions([]);
    setRevisionsError("");
    setRevisionsLoading(Boolean(value));
    onSelectionChange(value, "");
  };

  const selectBenchmarkCategory = (nextCategory) => {
    setBenchmarkType(nextCategory);
    if (nextCategory === "arduino") {
      setCases(ARDUINO_PRESET_CASES);
      setAutomatic(true);
    }
    // Standard runs no longer use preset cases: they execute against the real
    // DeepEval dataset via the sidecar, so no case editing applies.
    // The stage-1 target form depends on the category. If the new category needs
    // a target the user has not chosen, return to stage 1 so the matching target
    // form is completed before benchmark configuration. Stage-2 selections are
    // preserved and the user returns here after fixing the target.
    const targetMissing = nextCategory === "standard"
      ? !standardModelId
      : !tutorId || !revisionId;
    if (targetMissing) {
      setValidationError(nextCategory === "standard"
        ? "Standardized benchmarks evaluate an interaction model directly. Select one to continue."
        : "Tutor benchmarks evaluate a tutor revision. Select a tutor and revision to continue.");
      setStep(1);
    }
  };

  const selectStandardBenchmark = (nextKey) => {
    setSelectedStandardBenchmark(nextKey);
    // Task names are suite-specific; a stale selection would hard-fail on launch.
    setSelectedTasks([]);
    // Clamp shots to the new suite's ceiling (e.g. BBH max 3).
    const nextSuite = standardSuites.find((bench) => bench.key === nextKey);
    if (nextSuite?.maxShots !== undefined) {
      setNShots((current) => Math.min(current, nextSuite.maxShots));
    }
  };

  const validateStep = (stepNumber) => {
    if (stepNumber === 1 && benchmarkType !== "standard") {
      if (!selectedTutor) return "Select a tutor.";
      if (!selectedRevision) return "Select a tutor revision.";
      if (!isUsableRevision(selectedRevision)) return "This revision is not benchmark-ready. Activate a configured revision so its immutable snapshot can be captured.";
    }
    if (stepNumber === 1 && benchmarkType === "standard") {
      if (!standardModelId) return "Select an interaction model to evaluate.";
    }
    if (stepNumber === 2) {
      if (benchmarkType === "rag" && !selectedTemplate) {
        return "Select an enabled benchmark template.";
      }
      if (benchmarkType === "standard" && !selectedStandardBenchmark) {
        return "Select a standardized benchmark suite.";
      }
      if (benchmarkType === "standard") return "";
      if (!cases.length) return "Add at least one test case.";
      if (cases.some((runCase) => !runCase.inputText.trim())) return "Every case requires a question or input prompt.";
      if (!automatic && cases.some((runCase) => !runCase.actualOutput.trim())) return "Every existing-output case requires an actual answer.";
      if (cases.some((runCase) => {
        if (!runCase.metadataJson.trim()) return false;
        try { JSON.parse(runCase.metadataJson); return false; } catch { return true; }
      })) return "Case metadata must be valid JSON.";
      if (topK < 1 || topK > 10) return "Retrieval Top K must be between 1 and 10.";
      if (consistencyRuns < 1 || consistencyRuns > 10) return "Consistency runs must be between 1 and 10.";
      if (!automatic && consistencyRuns !== 1) return "Existing-answer evaluation requires consistency runs to be 1.";
      if (totalOutputs > 200) return "The expanded run cannot exceed 200 evaluated outputs.";
    }
    return "";
  };

  const goNext = () => {
    const nextError = validateStep(step);
    setValidationError(nextError);
    if (nextError) return;
    setStep((current) => Math.min(3, current + 1));
  };

  const updateCase = (index, value) => setCases((current) => current.map((runCase, position) => position === index ? value : runCase));
  const removeCase = (index) => setCases((current) => current.filter((_, position) => position !== index));
  const duplicateCase = (index) => setCases((current) => [...current.slice(0, index + 1), { ...current[index] }, ...current.slice(index + 1)]);
  const moveCase = (index, direction) => setCases((current) => {
    const target = index + direction;
    if (target < 0 || target >= current.length) return current;
    const next = [...current];
    [next[index], next[target]] = [next[target], next[index]];
    return next;
  });

  const setGenerationMode = (generate) => {
    setAutomatic(generate);
    if (!generate) setConsistencyRuns(1);
  };

  const launch = async () => {
    const errors = [1, 2, 3].map(validateStep).filter(Boolean);
    if (errors.length || submitting) {
      setValidationError(errors[0] ?? "");
      return;
    }
    setSubmitting(true);
    setLaunchError("");
    try {
      if (benchmarkType === "standard") {
        const created = await createStandardBenchmarkRun({
          interactionModelId: standardModelId,
          benchmarkKey: selectedStandardBenchmark,
          tasks: selectedTasks.length ? selectedTasks : null,
          nShots,
          nProblems,
        });
        onCreated(null, created.id, "standard");
        return;
      }
      const created = await createBenchmarkRun(tutorId, {
        benchmarkTemplateId: templateId,
        tutorRevisionId: revisionId,
        tutorConfigurationSnapshotId: selectedRevision?.latestSnapshotId || null,
        interactionModelId: selectedRevision?.interactionModelId || null,
        modelAlias: selectedRevision?.providerModelName || selectedRevision?.interactionModelName || null,
        generateActualOutputs: automatic,
        useTutorKnowledge: true,
        topK,
        consistencyRuns,
        cases: cases.map((runCase) => ({
          inputText: runCase.inputText.trim(),
          actualOutput: automatic ? "" : runCase.actualOutput.trim(),
          expectedOutput: runCase.expectedOutput.trim() || null,
          contextText: runCase.contextText.trim() || null,
          metadataJson: runCase.metadataJson.trim() || null,
        })),
      });
      onCreated(tutorId, created.id);
    } catch (error) {
      setLaunchError(getApiErrorDescription(error, "Could not launch the benchmark run."));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="benchmark-wizard">
      <Steps current={step - 1} items={STEP_LABELS.map((title) => ({ title }))} responsive style={{ marginBottom: 20 }} />

      {step === 1 ? (
        benchmarkType === "standard" ? (
          <Card title="Select target interaction model">
            <Typography.Paragraph type="secondary">Standardized benchmarks evaluate a model directly against public datasets. Tutor prompts and knowledge are not involved.</Typography.Paragraph>
            {interactionModelsError ? <Alert type="error" title={interactionModelsError} showIcon style={{ marginBottom: 16 }} /> : null}
            <Form layout="vertical" requiredMark={false}>
              <Form.Item label="Interaction model" required>
                <Select
                  showSearch
                  optionFilterProp="label"
                  value={standardModelId || undefined}
                  onChange={setStandardModelId}
                  loading={interactionModelsLoading}
                  placeholder="Select an interaction model"
                  options={interactionModels.map((model) => ({ value: model.id, label: `${model.name} · ${model.providerModelName || "Provider model not specified"}` }))}
                />
              </Form.Item>
            </Form>
            {selectedStandardModel ? (
              <Descriptions className="benchmark-selection-summary" bordered size="small" column={{ xs: 1, sm: 2 }}>
                <Descriptions.Item label="Model">{selectedStandardModel.name}</Descriptions.Item>
                <Descriptions.Item label="Provider model">{selectedStandardModel.providerModelName || "Not specified"}</Descriptions.Item>
                <Descriptions.Item label="Runtime engine">{selectedStandardModel.runtimeEngineName || "—"}</Descriptions.Item>
              </Descriptions>
            ) : null}
          </Card>
        ) : (
        <Card title="Select target tutor and revision">
          <Typography.Paragraph type="secondary">Choose the exact tutor configuration to evaluate with this run.</Typography.Paragraph>
          {tutorsError ? <Alert type="error" title={tutorsError} showIcon style={{ marginBottom: 16 }} /> : null}
          <Form layout="vertical" requiredMark={false}>
            <Form.Item label="Tutor" required extra={selectedTutor?.description}>
              <Select showSearch optionFilterProp="label" value={tutorId || undefined} onChange={selectTutor} loading={tutorsLoading} placeholder="Select a tutor" options={tutors.map((tutor) => ({ value: tutor.id, label: tutor.name }))} />
            </Form.Item>
            <Form.Item label="Revision" required>
              <Select
                value={revisionId || undefined}
                onChange={(value) => { setRevisionId(value); onSelectionChange(tutorId, value); }}
                loading={revisionsLoading}
                disabled={!tutorId}
                placeholder="Select a revision"
                options={revisions.filter((revision) => revisionStatus(revision.status) !== "Archived").map((revision) => ({
                  value: revision.id,
                  disabled: !isUsableRevision(revision),
                  label: `Revision ${revision.versionNumber} · ${revisionStatus(revision.status)}${revisionStatus(revision.status) === "Active" ? " · Active" : ""}${!isUsableRevision(revision) ? " · Configuration required" : ""}`,
                }))}
              />
            </Form.Item>
          </Form>
          {revisionsError ? <Alert type="error" title={revisionsError} showIcon /> : null}
          {!revisionsLoading && tutorId && !revisions.some(isUsableRevision) ? <Alert type="warning" title="This tutor does not have a benchmark-ready revision." action={<Button size="small" onClick={() => onConfigureTutor(tutorId)}>Configure tutor</Button>} showIcon /> : null}
          {selectedRevision ? (
            <Descriptions className="benchmark-selection-summary" bordered size="small" column={{ xs: 1, sm: 2 }}>
              <Descriptions.Item label="Status">{revisionStatus(selectedRevision.status)}</Descriptions.Item>
              <Descriptions.Item label="Interaction model">{selectedRevision.interactionModelName || selectedRevision.providerModelName || "Configured model"}</Descriptions.Item>
              <Descriptions.Item label="Model alias">{selectedRevision.providerModelName || "Resolved when launched"}</Descriptions.Item>
              <Descriptions.Item label="Activation slug">{selectedRevision.activationSlug || "Not active"}</Descriptions.Item>
            </Descriptions>
          ) : null}
          {selectedRevision && !isUsableRevision(selectedRevision) ? <Alert type="warning" title="This revision needs a valid prompt, an enabled interaction model, and an activation snapshot before it can be benchmarked." showIcon style={{ marginTop: 16 }} /> : null}
        </Card>
        )
      ) : null}

      {step === 2 ? (
        <Space orientation="vertical" size={16} style={{ width: "100%" }}>
        <Card title="1. Select Benchmark Category">
          <Radio.Group
            value={benchmarkType}
            onChange={(e) => selectBenchmarkCategory(e.target.value)}
            style={{ width: "100%" }}
          >
            <Row gutter={[16, 16]}>
              <Col xs={24} md={8}>
                <Card
                  size="small"
                  hoverable
                  onClick={() => selectBenchmarkCategory("rag")}
                  style={{ height: "100%", cursor: "pointer", borderColor: benchmarkType === "rag" ? "#1677ff" : undefined }}
                >
                  <Radio value="rag"><Typography.Text strong>Tutor RAG & Task Evaluation</Typography.Text></Radio>
                  <Typography.Paragraph type="secondary" style={{ margin: "8px 0 0 24px", fontSize: 13 }}>
                    Evaluate tutor answers, retrieval grounding, hallucination, and correctness against configured templates.
                  </Typography.Paragraph>
                </Card>
              </Col>
              <Col xs={24} md={8}>
                <Card
                  size="small"
                  hoverable={isAdmin}
                  onClick={() => isAdmin && selectBenchmarkCategory("standard")}
                  style={{ height: "100%", cursor: isAdmin ? "pointer" : "not-allowed", opacity: isAdmin ? 1 : 0.55, borderColor: benchmarkType === "standard" ? "#1677ff" : undefined }}
                >
                  <Radio value="standard" disabled={!isAdmin}><Typography.Text strong>Standard LLM Benchmarks</Typography.Text>{!isAdmin ? <Tag color="default" style={{ marginLeft: 8 }}>Admin only</Tag> : null}</Radio>
                  <Typography.Paragraph type="secondary" style={{ margin: "8px 0 0 24px", fontSize: 13 }}>
                    Zero-sandbox standardized datasets (MMLU, GSM8K, BBH, HellaSwag, DROP, TruthfulQA) for raw model reasoning.
                  </Typography.Paragraph>
                </Card>
              </Col>
              <Col xs={24} md={8}>
                <Card
                  size="small"
                  hoverable
                  onClick={() => selectBenchmarkCategory("arduino")}
                  style={{ height: "100%", cursor: "pointer", borderColor: benchmarkType === "arduino" ? "#1677ff" : undefined }}
                >
                  <Radio value="arduino"><Typography.Text strong>Custom Hardware / STEM Diagnostics</Typography.Text></Radio>
                  <Typography.Paragraph type="secondary" style={{ margin: "8px 0 0 24px", fontSize: 13 }}>
                    Multi-modal visual circuit diagnostic suite (Arduino board schematics + code + Socratic guidance quality).
                  </Typography.Paragraph>
                </Card>
              </Col>
            </Row>
          </Radio.Group>
        </Card>

          {benchmarkType === "rag" ? (
            <Card title="2. Evaluation Template & Metrics">
              <Typography.Paragraph type="secondary">Templates define which deterministic local indicators and qualitative metrics are calculated.</Typography.Paragraph>
              {templatesLoading ? <Spin description="Loading templates" /> : null}
              {templatesError ? <Alert type="error" title={templatesError} showIcon /> : null}
              {!templatesLoading && !templatesError && !templates.length ? (
                <Empty description="No enabled benchmark templates">
                  <Button type="primary" onClick={onOpenTemplates}>Open templates</Button>
                </Empty>
              ) : null}
              {templates.length ? (
                <Radio.Group className="benchmark-template-options" value={templateId} onChange={(event) => setTemplateId(event.target.value)}>
                  {templates.map((template) => (
                    <label key={template.id} className={`benchmark-template-option ${templateId === template.id ? "selected" : ""}`}>
                      <Radio value={template.id} />
                      <span className="benchmark-template-copy">
                        <Typography.Text strong>{template.name}</Typography.Text>
                        <Typography.Text type="secondary">{template.description || "No description provided."}</Typography.Text>
                        <Flex gap={6} wrap>{parseMetrics(template.metricsJson).map((metric) => <Tag key={metric.key}>{metricName(metric.key)} · {formatPercent(metric.threshold)}</Tag>)}</Flex>
                      </span>
                    </label>
                  ))}
                </Radio.Group>
              ) : null}
              {selectedMetrics.length ? (
                <div className="benchmark-metric-list" style={{ marginTop: 16 }}>
                  {selectedMetrics.map((metric) => (
                    <div className="benchmark-metric-summary" key={metric.key}>
                      <Typography.Text strong>{metricName(metric.key)} · threshold {formatPercent(metric.threshold)}</Typography.Text>
                      <Typography.Text type="secondary">{METRIC_DETAILS[metric.key]?.description}</Typography.Text>
                    </div>
                  ))}
                </div>
              ) : null}
            </Card>
          ) : null}

          {benchmarkType === "standard" ? (
            <Card title="2. Standardized Benchmark Suite Options">
              <Typography.Paragraph type="secondary">Select public dataset to evaluate against target model:</Typography.Paragraph>
              {standardSuitesError ? <Alert type="error" title={standardSuitesError} showIcon style={{ marginBottom: 16 }} /> : null}
              {standardSuitesLoading ? <Spin description="Loading suites" /> : null}
              {!standardSuitesLoading && !standardSuitesError ? (
                <Radio.Group value={selectedStandardBenchmark} onChange={(e) => selectStandardBenchmark(e.target.value)} style={{ width: "100%" }}>
                  <Row gutter={[12, 12]}>
                    {standardSuites.map((bench) => (
                      <Col xs={24} sm={12} key={bench.key}>
                        <Card
                          size="small"
                          hoverable={bench.enabled}
                          onClick={() => bench.enabled && selectStandardBenchmark(bench.key)}
                          style={{ cursor: bench.enabled ? "pointer" : "not-allowed", opacity: bench.enabled ? 1 : 0.55, borderColor: selectedStandardBenchmark === bench.key ? "#1677ff" : undefined }}
                        >
                          <Radio value={bench.key} disabled={!bench.enabled}>
                            <Typography.Text strong>{bench.displayName}</Typography.Text>
                            {!bench.enabled ? <Tag color="default" style={{ marginLeft: 8 }}>Deferred</Tag> : null}
                          </Radio>
                          <Typography.Paragraph type="secondary" style={{ margin: "4px 0 0 24px", fontSize: 12 }}>
                            {bench.description}
                            {!bench.enabled ? " Requires a code-execution sandbox; not selectable." : ""}
                          </Typography.Paragraph>
                        </Card>
                      </Col>
                    ))}
                  </Row>
                </Radio.Group>
              ) : null}
              <Form layout="vertical" style={{ marginTop: 16 }}>
                <Row gutter={16}>
                  <Col xs={24} md={8}>
                    <Form.Item
                      label="Few-shot examples (n_shots)"
                      extra={selectedStandardSuite && !selectedStandardSuite.supportsShots ? "Not supported by this suite; value is ignored." : `0 = zero-shot. Maximum ${selectedStandardSuite?.maxShots ?? 5} for this suite.`}
                    >
                      <InputNumber min={0} max={selectedStandardSuite?.maxShots ?? 5} value={nShots} onChange={setNShots} disabled={selectedStandardSuite ? !selectedStandardSuite.supportsShots : false} />
                    </Form.Item>
                  </Col>
                  <Col xs={24} md={8}>
                    <Form.Item
                      label="Problem cap (n_problems)"
                      extra={selectedStandardSuite && !selectedStandardSuite.supportsProblemCap
                        ? "Not supported by this suite; the full dataset runs."
                        : "0 = full dataset (slow). Positive values cap problems per task on task-based suites."}
                    >
                      <InputNumber min={0} max={10000} value={nProblems} onChange={setNProblems} disabled={selectedStandardSuite ? !selectedStandardSuite.supportsProblemCap : false} />
                    </Form.Item>
                  </Col>
                  {selectedStandardSuite?.supportsTasks ? (
                    <Col xs={24}>
                      <Form.Item
                        label="Tasks"
                        extra="Optional. Leave empty to run the full default task set. Unknown names are rejected by the sidecar."
                      >
                        <Select
                          mode="multiple"
                          allowClear
                          showSearch
                          placeholder="All tasks (default)"
                          value={selectedTasks}
                          onChange={setSelectedTasks}
                          maxTagCount="responsive"
                          options={(STANDARD_BENCHMARK_TASKS[selectedStandardBenchmark] ?? []).map((task) => ({ value: task, label: task }))}
                        />
                      </Form.Item>
                    </Col>
                  ) : null}
                </Row>
                {nProblems === 0 ? <Alert type="warning" title="A problem cap of 0 runs the full default dataset. This can issue thousands of model calls and take hours." showIcon /> : null}
              </Form>
            </Card>
          ) : null}

          {benchmarkType === "arduino" ? (
            <Card title="2. Arduino Hardware Diagnostic Suite (Skeleton)">
              <Typography.Paragraph type="secondary">Multimodal evaluation covering visual pinout detection, circuit comprehension, and pedagogical guidance quality.</Typography.Paragraph>
              <Alert type="info" title="4-Stage Weighted Evaluation: Detail Detection (20%), Circuit Comprehension (30%), Solution Reasoning (30%), Guidance Pedagogy (20%)." showIcon style={{ marginBottom: 16 }} />
              <Descriptions bordered size="small" column={1}>
                <Descriptions.Item label="Target Domain">Arduino Uno / Nano STEM Troubleshooting</Descriptions.Item>
                <Descriptions.Item label="Input Modality">Image (schematic/breadboard) + Prompt + C++ Code</Descriptions.Item>
                <Descriptions.Item label="Minor Safety Guardrails">COPPA/GDPR-K Child Safety + Socratic Non-Leaking Guidance</Descriptions.Item>
              </Descriptions>
            </Card>
          ) : null}

          {benchmarkType !== "standard" ? (
            <>
              <Card title="3. Test cases and execution">
                <Form layout="vertical">
                  <Form.Item label="Answer source">
                    <Radio.Group value={automatic} onChange={(event) => setGenerationMode(event.target.value)}>
                      <Space orientation="vertical">
                        <Radio value>Generate tutor answers automatically</Radio>
                        <Radio value={false}>Evaluate existing answers (advanced)</Radio>
                      </Space>
                    </Radio.Group>
                  </Form.Item>
                  <Alert type="info" title="Expected answers enable correctness scoring. Automatic answers use the activated tutor prompt, models, parameters, and knowledge scope." showIcon style={{ marginBottom: 16 }} />
                  <Row gutter={16}>
                    <Col xs={24} md={8}><Form.Item label="Tutor knowledge"><Typography.Text>Activated configuration</Typography.Text></Form.Item></Col>
                    <Col xs={12} md={8}><Form.Item label="Retrieval Top K"><InputNumber min={1} max={10} value={topK} onChange={setTopK} style={{ width: "100%" }} /></Form.Item></Col>
                    <Col xs={12} md={8}><Form.Item label="Consistency runs"><InputNumber min={1} max={10} value={consistencyRuns} onChange={setConsistencyRuns} disabled={!automatic} style={{ width: "100%" }} /></Form.Item></Col>
                  </Row>
                </Form>
                {automatic && consistencyRuns > 1 ? <Alert type="warning" title={`${cases.length} questions × ${consistencyRuns} repeats = ${totalOutputs} evaluated outputs.`} showIcon /> : null}
              </Card>
              {cases.map((runCase, index) => <BenchmarkCaseEditor key={index} runCase={runCase} index={index} total={cases.length} automatic={automatic} onChange={updateCase} onRemove={removeCase} onDuplicate={duplicateCase} onMove={moveCase} />)}
              <Button icon={<PlusOutlined />} onClick={() => setCases((current) => [...current, emptyBenchmarkCase()])} disabled={cases.length >= 200}>Add case</Button>
            </>
          ) : null}
        </Space>
      ) : null}

      {step === 3 ? (
        <Card title="Review and launch">
          {benchmarkType === "standard" ? (
            <Descriptions bordered column={{ xs: 1, sm: 2 }}>
              <Descriptions.Item label="Model">{selectedStandardModel ? `${selectedStandardModel.name} · ${selectedStandardModel.providerModelName || "Provider model not specified"}` : "—"}</Descriptions.Item>
              <Descriptions.Item label="Category">Standard LLM Benchmark</Descriptions.Item>
              <Descriptions.Item label="Suite">{selectedStandardSuite?.displayName || selectedStandardBenchmark}</Descriptions.Item>
              <Descriptions.Item label="Few-shot examples">{nShots}</Descriptions.Item>
              <Descriptions.Item label="Problem cap">{nProblems === 0 ? "None (full dataset)" : `${nProblems} per task`}</Descriptions.Item>
              {selectedStandardSuite?.supportsTasks ? (
                <Descriptions.Item label="Tasks">{selectedTasks.length ? `${selectedTasks.length} selected` : "All (default set)"}</Descriptions.Item>
              ) : null}
              <Descriptions.Item label="Execution">Runs asynchronously against the model's runtime engine.</Descriptions.Item>
            </Descriptions>
          ) : (
            <Descriptions bordered column={{ xs: 1, sm: 2 }}>
              <Descriptions.Item label="Tutor">{selectedTutor?.name}</Descriptions.Item>
              <Descriptions.Item label="Revision">Revision {selectedRevision?.versionNumber} · {revisionStatus(selectedRevision?.status)}</Descriptions.Item>
              <Descriptions.Item label="Model">{selectedRevision?.providerModelName || selectedRevision?.interactionModelName || "Configured model"}</Descriptions.Item>
              <Descriptions.Item label="Category">{benchmarkType === "rag" ? "Tutor RAG Evaluation" : "Arduino Diagnostic"}</Descriptions.Item>
              <Descriptions.Item label="Template / Suite">{benchmarkType === "rag" ? (selectedTemplate?.name || "None") : "Arduino 15-Question Suite"}</Descriptions.Item>
              <Descriptions.Item label="Metrics">{benchmarkType === "rag" ? (selectedMetrics.map((metric) => metricName(metric.key)).join(", ") || "Default quality metrics") : "Standard dataset scoring"}</Descriptions.Item>
              <Descriptions.Item label="Logical questions">{cases.length}</Descriptions.Item>
              <Descriptions.Item label="Consistency runs">{consistencyRuns}</Descriptions.Item>
            </Descriptions>
          )}
        </Card>
      ) : null}

      {validationError ? <Alert type="warning" title={validationError} showIcon /> : null}
      {launchError ? <Alert type="error" title={launchError} showIcon /> : null}
      <Flex justify="space-between">
        <Button onClick={() => { setValidationError(""); setStep((current) => Math.max(1, current - 1)); }} disabled={step === 1 || submitting}>Back</Button>
        {step < 3
          ? <Button type="primary" onClick={goNext}>Continue</Button>
          : <Button type="primary" onClick={launch} loading={submitting} disabled={Boolean(validateStep(3))}>Launch benchmark</Button>}
      </Flex>
    </div>
  );
}
