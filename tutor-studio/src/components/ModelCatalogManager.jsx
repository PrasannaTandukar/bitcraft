import { PlusOutlined, ReloadOutlined, SearchOutlined } from "@ant-design/icons";
import { Alert, Button, Drawer, Empty, Flex, Form, Input, InputNumber, Select, Space, Switch, Table, Tag, Typography } from "antd";
import { useCallback, useEffect, useMemo, useState } from "react";
import { apiFetch, getApiErrorDescription } from "../api/client.js";
import PageHeader from "./PageHeader.jsx";

const ENGINES_PATH = "/api/admin/runtime-engines";

const catalogConfig = {
  embedding: {
    apiPath: "/api/admin/embedding-models",
    title: "Model catalog",
    description: "Manage embedding models used for document vectorization and tutor knowledge retrieval.",
    singular: "embedding model",
    providerPlaceholder: "text-embedding-3-small",
  },
  interaction: {
    apiPath: "/api/admin/interaction-models",
    title: "Interaction models",
    description: "Manage the language models that power active tutor conversations and benchmark generation.",
    singular: "interaction model",
    providerPlaceholder: "gpt-4o",
  },
};

export default function ModelCatalogManager({ kind }) {
  const config = catalogConfig[kind];
  const isEmbedding = kind === "embedding";
  const [form] = Form.useForm();
  const [models, setModels] = useState([]);
  const [engines, setEngines] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");
  const [engineError, setEngineError] = useState("");
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [search, setSearch] = useState("");

  const load = useCallback(async (refresh = false) => {
    if (refresh) setRefreshing(true); else setLoading(true);
    const [modelsResult, enginesResult] = await Promise.allSettled([apiFetch(config.apiPath), apiFetch(ENGINES_PATH)]);
    if (modelsResult.status === "fulfilled") {
      setModels(modelsResult.value ?? []);
      setError("");
    } else setError(getApiErrorDescription(modelsResult.reason, `Could not load ${config.title.toLowerCase()}.`));
    if (enginesResult.status === "fulfilled") {
      setEngines((enginesResult.value ?? []).filter((engine) => engine.isEnabled));
      setEngineError("");
    } else setEngineError(getApiErrorDescription(enginesResult.reason, "Runtime engines could not be loaded."));
    setLoading(false);
    setRefreshing(false);
  }, [config]);

  useEffect(() => {
    const timer = window.setTimeout(() => load(), 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  const filteredModels = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!query) return models;
    return models.filter((model) => [model.name, model.providerModelName, model.runtimeEngineName].some((value) => value?.toLowerCase().includes(query)));
  }, [models, search]);

  const close = () => {
    if (saving) return;
    setOpen(false);
    setFormError("");
    form.resetFields();
  };

  const save = async (values) => {
    setSaving(true);
    setFormError("");
    try {
      const payload = {
        name: values.name.trim(),
        runtimeEngineId: values.runtimeEngineId,
        providerModelName: values.providerModelName?.trim() || null,
        settingsJson: values.settingsJson?.trim() || null,
        isEnabled: values.isEnabled,
      };
      if (isEmbedding) payload.embeddingDimensions = Number(values.embeddingDimensions);
      const created = await apiFetch(config.apiPath, { method: "POST", json: payload });
      setModels((current) => [created, ...current]);
      setOpen(false);
      form.resetFields();
    } catch (requestError) {
      setFormError(getApiErrorDescription(requestError, `Could not create the ${config.singular}.`));
    } finally {
      setSaving(false);
    }
  };

  const columns = [
    { title: "Name", dataIndex: "name", sorter: (a, b) => a.name.localeCompare(b.name), render: (value, model) => <div className="management-name-cell"><Typography.Text strong>{value}</Typography.Text><Typography.Text type="secondary">{model.providerModelName || "Provider model not specified"}</Typography.Text></div> },
    { title: "Runtime", dataIndex: "runtimeEngineName", width: 180, render: (value) => value || "—" },
    ...(isEmbedding ? [{ title: "Dimensions", dataIndex: "embeddingDimensions", width: 130, sorter: (a, b) => a.embeddingDimensions - b.embeddingDimensions, render: (value) => value > 0 ? Number(value).toLocaleString() : "—" }] : []),
    { title: "Status", dataIndex: "isEnabled", width: 110, filters: [{ text: "Enabled", value: true }, { text: "Disabled", value: false }], onFilter: (value, record) => record.isEnabled === value, render: (value) => <Tag color={value ? "success" : "default"}>{value ? "Enabled" : "Disabled"}</Tag> },
  ];

  return (
    <main className="workspace-page">
      <div className="workspace-page-inner">
        <PageHeader
          title={config.title}
          description={config.description}
          extra={[<Button key="refresh" icon={<ReloadOutlined />} onClick={() => load(true)} loading={refreshing}>Refresh</Button>, <Button key="add" type="primary" icon={<PlusOutlined />} onClick={() => setOpen(true)}>Add model</Button>]}
        />
        {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}
        <Input className="management-search" prefix={<SearchOutlined />} allowClear value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search models" />
        <Table className="workspace-table" rowKey="id" columns={columns} dataSource={filteredModels} loading={loading} scroll={{ x: 680 }} pagination={{ pageSize: 10, showSizeChanger: true }} locale={{ emptyText: <Empty description={`No ${config.title.toLowerCase()} found`} /> }} />
      </div>

      <Drawer
        title={`Add ${config.singular}`}
        open={open}
        onClose={close}
        size={560}
        destroyOnHidden
        footer={<Space style={{ width: "100%", justifyContent: "flex-end" }}><Button onClick={close} disabled={saving}>Cancel</Button><Button type="primary" onClick={() => form.submit()} loading={saving} disabled={!engines.length}>Save model</Button></Space>}
      >
        {formError ? <Alert type="error" title={formError} showIcon style={{ marginBottom: 16 }} /> : null}
        {engineError ? <Alert type="error" title={engineError} showIcon style={{ marginBottom: 16 }} /> : null}
        {!engines.length && !engineError ? <Alert type="warning" title="No enabled runtime engines are available. Add or enable a runtime engine first." showIcon style={{ marginBottom: 16 }} /> : null}
        <Form form={form} layout="vertical" onFinish={save} requiredMark={false} initialValues={{ isEnabled: true, embeddingDimensions: 0 }}>
          <Form.Item label="Name" name="name" rules={[{ required: true, whitespace: true, message: "Enter a model name." }]}><Input placeholder={`My ${isEmbedding ? "Embedding" : "Interaction"} Model`} /></Form.Item>
          <Form.Item label="Runtime engine" name="runtimeEngineId" rules={[{ required: true, message: "Select a runtime engine." }]}><Select placeholder="Select an enabled runtime engine" options={engines.map((engine) => ({ value: engine.id, label: engine.name }))} /></Form.Item>
          <Form.Item label="Provider model name" name="providerModelName"><Input placeholder={config.providerPlaceholder} /></Form.Item>
          {isEmbedding ? <Form.Item label="Embedding dimensions" name="embeddingDimensions"><InputNumber min={0} precision={0} style={{ width: "100%" }} /></Form.Item> : null}
          <Form.Item label="Settings JSON" name="settingsJson"><Input.TextArea className="workspace-code" autoSize={{ minRows: 4, maxRows: 12 }} placeholder='{"temperature": 0.7}' /></Form.Item>
          <Form.Item label="Enabled" name="isEnabled" valuePropName="checked"><Switch /></Form.Item>
        </Form>
      </Drawer>
    </main>
  );
}
