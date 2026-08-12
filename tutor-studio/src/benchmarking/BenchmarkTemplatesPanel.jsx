import { DeleteOutlined, PlusOutlined, ReloadOutlined } from "@ant-design/icons";
import { Alert, Button, Checkbox, Collapse, Drawer, Empty, Flex, Form, Input, InputNumber, Modal, Space, Switch, Table, Tag, Typography } from "antd";
import { useEffect, useState } from "react";
import { getApiErrorDescription } from "../api/client.js";
import { createBenchmarkTemplate, deleteBenchmarkTemplate, listBenchmarkTemplates } from "./benchmarkingApi.js";
import { formatPercent, METRIC_DETAILS, metricName, parseMetrics } from "./benchmarkingUtils.js";

const DEFAULT_METRICS = Object.keys(METRIC_DETAILS).map((key) => ({ key, threshold: 0.5, enabled: true }));

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
    { title: "Status", dataIndex: "isEnabled", width: 110, filters: [{ text: "Enabled", value: true }, { text: "Disabled", value: false }], onFilter: (value, record) => record.isEnabled === value, render: (value) => <Tag color={value ? "success" : "default"}>{value ? "Enabled" : "Disabled"}</Tag> },
    { title: "Metrics and thresholds", dataIndex: "metricsJson", render: (value) => { const parsed = parseMetrics(value); return parsed.length ? <Flex gap={6} wrap>{parsed.map((metric) => <Tag key={metric.key}>{metricName(metric.key)} · {formatPercent(metric.threshold)}</Tag>)}</Flex> : <Typography.Text type="secondary">Malformed or empty metrics payload</Typography.Text>; } },
    { title: "Guidance metadata", dataIndex: "guidancePath", width: 220, render: (value) => value ? <div><Typography.Text>{value}</Typography.Text><br /><Typography.Text type="secondary">Metadata only</Typography.Text></div> : "—" },
    { title: "", key: "action", width: 80, fixed: "right", render: (_, template) => <Button danger type="text" aria-label={`Delete ${template.name}`} icon={<DeleteOutlined />} onClick={() => { setDeleteError(""); setDeleteTarget(template); }} /> },
  ];

  return (
    <div className="benchmark-panel-stack">
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
      <Table className="workspace-table" rowKey="id" columns={columns} dataSource={templates} loading={loading} scroll={{ x: 900 }} pagination={{ pageSize: 10 }} locale={{ emptyText: <Empty description="No benchmark templates" /> }} />

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
          <Space orientation="vertical" size={12} style={{ width: "100%" }}>
            {metrics.map((metric) => (
              <div className="benchmark-metric-control" key={metric.key}>
                <Checkbox checked={metric.enabled} onChange={(event) => setMetrics((current) => current.map((item) => item.key === metric.key ? { ...item, enabled: event.target.checked } : item))}>{metricName(metric.key)}</Checkbox>
                <InputNumber aria-label={`${metricName(metric.key)} threshold`} min={0} max={1} step={0.05} value={metric.threshold} disabled={!metric.enabled} onChange={(value) => setMetrics((current) => current.map((item) => item.key === metric.key ? { ...item, threshold: value } : item))} />
                <Typography.Text type="secondary">{METRIC_DETAILS[metric.key].description}</Typography.Text>
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
}
