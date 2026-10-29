import { DeleteOutlined, PlusOutlined, ReloadOutlined } from "@ant-design/icons";
import { Alert, Button, Checkbox, Collapse, Drawer, Empty, Flex, Form, Input, InputNumber, Modal, Space, Switch, Table, Tabs, Tag, Typography } from "antd";
import { useEffect, useState } from "react";
import { getApiErrorDescription } from "../api/client.js";
import InfoTooltip from "../components/InfoTooltip.jsx";
import BenchmarkingHelp from "./BenchmarkingHelp.jsx";
import { createBenchmarkTemplate, deleteBenchmarkTemplate, listBenchmarkTemplates, listStandardBenchmarkSuites } from "./benchmarkingApi.js";
import { formatPercent, METRIC_DETAILS, metricName, metricTier, parseMetrics } from "./benchmarkingUtils.js";

// WS7: group the metric picker by tier so deterministic and judge-backed
// metrics are not mixed in one flat list.
const METRIC_TIER_GROUPS = [
  { tier: 1, label: "Tier 1 · Deterministic" },
  { tier: 2, label: "Tier 2 · LLM judge" },
];

const DEFAULT_METRICS = Object.keys(METRIC_DETAILS).map((key) => ({ key, threshold: 0.5, enabled: true }));

// WS5: custom benchmarks shipped with the sidecar are Bitcraft-authored, not
// user-authored (decision D-2). They are identified by key here and rendered
// read-only.
const CUSTOM_BENCHMARK_KEYS = ["arduino"];

const CUSTOM_BENCHMARK_COLUMNS = [
  { title: "Benchmark", dataIndex: "displayName", render: (value, suite) => <div className="benchmark-template-name"><Typography.Text strong>{value}</Typography.Text><Typography.Text type="secondary">{suite.key}</Typography.Text></div> },
  { title: "Description", dataIndex: "description" },
  { title: "Modality", dataIndex: "modality", width: 110, render: (value) => <Tag>{value || "text"}</Tag> },
  { title: "Status", dataIndex: "enabled", width: 100, render: (value) => <Tag color={value ? "success" : "default"}>{value ? "Enabled" : "Disabled"}</Tag> },
];

export default function BenchmarkTemplatesPanel() {
  const [form] = Form.useForm();
  const [templates, setTemplates] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [refreshNonce, setRefreshNonce] = useState(0);
  const [showCreate, setShowCreate] = useState(false);
  const [metrics, setMetrics] = useState(DEFAULT_METRICS);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState("");
  const [customSuites, setCustomSuites] = useState([]);
  const [customLoading, setCustomLoading] = useState(true);
  const [customError, setCustomError] = useState("");
  const [customNonce, setCustomNonce] = useState(0);

  // WS6: custom benchmarks are read-only (decision D-2). Load them for display only.
  useEffect(() => {
    const controller = new AbortController();
    listStandardBenchmarkSuites(controller.signal)
      .then((data) => {
        setCustomSuites((data ?? []).filter((suite) => CUSTOM_BENCHMARK_KEYS.includes(suite.key)));
        setCustomError("");
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setCustomError(getApiErrorDescription(requestError, "Could not load custom benchmarks."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setCustomLoading(false);
      });
    return () => controller.abort();
  }, [customNonce]);

  useEffect(() => {
    const controller = new AbortController();
    listBenchmarkTemplates(controller.signal)
      .then((data) => {
        setTemplates(data ?? []);
        setError("");
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Could not load benchmark templates."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [refreshNonce]);

  const refresh = () => {
    setLoading(true);
    setRefreshNonce((value) => value + 1);
  };

  const closeCreate = () => {
    if (saving) return;
    setShowCreate(false);
    form.resetFields();
    setMetrics(DEFAULT_METRICS);
    setFormError("");
  };

  const saveTemplate = async (values) => {
    const enabledMetrics = metrics.filter((metric) => metric.enabled);
    if (!enabledMetrics.length) {
      setFormError("Enable at least one metric.");
      return;
    }
    setSaving(true);
    setFormError("");
    try {
      const created = await createBenchmarkTemplate({
        key: values.key.trim(),
        name: values.name.trim(),
        description: values.description?.trim() || null,
        guidancePath: values.guidancePath?.trim() || null,
        metricsJson: JSON.stringify(enabledMetrics),
        isEnabled: values.isEnabled,
      });
      setTemplates((current) => [...current, created].sort((left, right) => left.name.localeCompare(right.name)));
      setShowCreate(false);
      form.resetFields();
      setMetrics(DEFAULT_METRICS);
    } catch (requestError) {
      setFormError(getApiErrorDescription(requestError, "Could not create the benchmark template."));
    } finally {
      setSaving(false);
    }
  };

  const confirmDelete = async () => {
    if (!deleteTarget || deleting) return;
    setDeleting(true);
    setDeleteError("");
    try {
      await deleteBenchmarkTemplate(deleteTarget.id);
      setTemplates((current) => current.filter((template) => template.id !== deleteTarget.id));
      setDeleteTarget(null);
    } catch (requestError) {
      setDeleteError(getApiErrorDescription(requestError, "This template could not be deleted. It may already be used by a benchmark run."));
      throw requestError;
    } finally {
      setDeleting(false);
    }
  };

  const columns = [
    { title: "Template", key: "name", render: (_, template) => <div className="benchmark-template-name"><Typography.Text strong>{template.name}</Typography.Text><Typography.Text type="secondary">{template.key}</Typography.Text>{template.description ? <Typography.Text>{template.description}</Typography.Text> : null}</div> },
    { title: "Status", dataIndex: "isEnabled", width: 100, filters: [{ text: "Enabled", value: true }, { text: "Disabled", value: false }], onFilter: (value, record) => record.isEnabled === value, render: (value) => <Tag color={value ? "success" : "default"}>{value ? "Enabled" : "Disabled"}</Tag> },
    { title: "Metrics & thresholds", dataIndex: "metricsJson", render: (value) => { const parsed = parseMetrics(value); return parsed.length ? <Flex gap={6} wrap>{parsed.map((metric) => <Tag key={metric.key}>{metricName(metric.key)} · {formatPercent(metric.threshold)}</Tag>)}</Flex> : <Typography.Text type="secondary">Malformed or empty metrics payload</Typography.Text>; } },
    { title: "Guidance", dataIndex: "guidancePath", width: 180, ellipsis: true, render: (value) => value ? <Typography.Text ellipsis={{ tooltip: value }}>{value}</Typography.Text> : "—" },
    { title: "", key: "action", width: 50, align: "right", render: (_, template) => <Button danger type="text" aria-label={`Delete ${template.name}`} title={`Delete ${template.name}`} icon={<DeleteOutlined />} onClick={() => { setDeleteError(""); setDeleteTarget(template); }} /> },
  ];

  const metricsTab = (
    <div className="benchmark-panel-stack">
      <BenchmarkingHelp
        title="How templates and thresholds work"
        steps={[
          "Create a template and enable the metrics you want to score.",
          "Set a threshold for each metric between 0 and 1.",
          "A case passes when every enabled metric meets its threshold.",
          "Tier 1 metrics are deterministic and need no judge model.",
          "Tier 2 metrics use a judge model and cost extra calls.",
        ]}
        note="Raise a threshold to demand stricter quality. Lower it to accept more answers."
      />
      <Flex justify="space-between" align="flex-start" gap={16} wrap>
        <div>
          <Typography.Title level={4}>Benchmark templates</Typography.Title>
          <Typography.Paragraph type="secondary">Reusable metric and threshold profiles for deterministic local evaluation.</Typography.Paragraph>
        </div>
        <Flex gap={8} wrap>
          <Button icon={<ReloadOutlined />} onClick={refresh} loading={loading}>Refresh</Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setShowCreate(true)}>Create template</Button>
        </Flex>
      </Flex>
      {error ? <Alert type="error" title={error} showIcon /> : null}
      <Table className="workspace-table" rowKey="id" columns={columns} dataSource={templates} loading={loading} size="middle" pagination={{ pageSize: 10 }} locale={{ emptyText: <Empty description="No benchmark templates" /> }} />

      <Drawer
        title="Create benchmark template"
        open={showCreate}
        onClose={closeCreate}
        size={680}
        destroyOnHidden
        footer={<Space style={{ width: "100%", justifyContent: "flex-end" }}><Button onClick={closeCreate} disabled={saving}>Cancel</Button><Button type="primary" loading={saving} onClick={() => form.submit()}>Create template</Button></Space>}
      >
        {formError ? <Alert type="error" title={formError} showIcon style={{ marginBottom: 16 }} /> : null}
        <Form form={form} layout="vertical" onFinish={saveTemplate} requiredMark={false} initialValues={{ isEnabled: true }}>
          <Form.Item label="Key" name="key" rules={[{ required: true, whitespace: true, message: "Enter a template key." }]}><Input placeholder="my-evaluation-profile" /></Form.Item>
          <Form.Item label="Name" name="name" rules={[{ required: true, whitespace: true, message: "Enter a template name." }]}><Input /></Form.Item>
          <Form.Item label="Description" name="description"><Input.TextArea rows={3} /></Form.Item>

          <Typography.Title level={5}>Metrics and thresholds</Typography.Title>
          <Space orientation="vertical" size={16} style={{ width: "100%" }}>
            {METRIC_TIER_GROUPS.map((group) => (
              <div key={group.tier}>
                <Typography.Text strong>{group.label}</Typography.Text>
                <Space orientation="vertical" size={12} style={{ width: "100%", marginTop: 8 }}>
                  {metrics.filter((metric) => metricTier(metric.key) === group.tier).map((metric) => (
                    <div className="benchmark-metric-control" key={metric.key}>
                      <Checkbox checked={metric.enabled} onChange={(event) => setMetrics((current) => current.map((item) => item.key === metric.key ? { ...item, enabled: event.target.checked } : item))}>{metricName(metric.key)}</Checkbox>
                      <InputNumber aria-label={`${metricName(metric.key)} threshold`} min={0} max={1} step={0.05} value={metric.threshold} disabled={!metric.enabled} onChange={(value) => setMetrics((current) => current.map((item) => item.key === metric.key ? { ...item, threshold: value } : item))} />
                      <InfoTooltip title={metricName(metric.key)} text={`${METRIC_DETAILS[metric.key].description} ${METRIC_DETAILS[metric.key].derivation}`}>
                        <Typography.Text type="secondary">{METRIC_DETAILS[metric.key].description}</Typography.Text>
                      </InfoTooltip>
                    </div>
                  ))}
                </Space>
              </div>
            ))}
          </Space>

          <Collapse ghost items={[{ key: "advanced", label: "Advanced metadata", children: <Form.Item label="Guidance path" name="guidancePath" extra="Stored as metadata only. The deterministic scorer does not load or apply this path."><Input /></Form.Item> }]} />
          <Form.Item label="Enabled" name="isEnabled" valuePropName="checked"><Switch /></Form.Item>
        </Form>
      </Drawer>

      <Modal title="Delete benchmark template" open={Boolean(deleteTarget)} onCancel={() => setDeleteTarget(null)} onOk={confirmDelete} okText="Delete template" okButtonProps={{ danger: true }} confirmLoading={deleting} closable={!deleting}>
        {deleteError ? <Alert type="error" title={deleteError} showIcon style={{ marginBottom: 16 }} /> : null}
        <Typography.Paragraph>Delete <strong>{deleteTarget?.name}</strong>? Templates already used by benchmark runs cannot be deleted.</Typography.Paragraph>
      </Modal>
    </div>
  );

  // WS6: read-only. No create, edit, or delete control (decision D-2).
  const customTab = (
    <div className="benchmark-panel-stack">
      <Flex justify="space-between" align="flex-start" gap={16} wrap>
        <div>
          <Typography.Title level={4}>Custom benchmarks</Typography.Title>
          <Typography.Paragraph type="secondary">Benchmarks shipped with the platform. Bitcraft develops these suites; they are not editable.</Typography.Paragraph>
        </div>
        <Button icon={<ReloadOutlined />} onClick={() => { setCustomLoading(true); setCustomNonce((value) => value + 1); }} loading={customLoading}>Refresh</Button>
      </Flex>
      {customError ? <Alert type="error" title={customError} showIcon /> : null}
      <Table className="workspace-table" rowKey="key" columns={CUSTOM_BENCHMARK_COLUMNS} dataSource={customSuites} loading={customLoading} size="middle" pagination={false} locale={{ emptyText: <Empty description="No custom benchmarks loaded" /> }} />
    </div>
  );

  return (
    <Tabs
      items={[
        { key: "metrics", label: "Metrics & RAG", children: metricsTab },
        { key: "custom", label: "Custom benchmarks", children: customTab },
      ]}
    />
  );
}
