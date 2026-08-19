import { DeleteOutlined, EditOutlined, PlusOutlined, ReloadOutlined, SearchOutlined } from "@ant-design/icons";
import { Alert, App, Button, Drawer, Empty, Form, Input, InputNumber, Popconfirm, Select, Space, Switch, Table, Tag, Typography } from "antd";
import { useCallback, useEffect, useMemo, useState } from "react";
import { apiFetch, getApiErrorDescription } from "../api/client.js";
import useRolePermissions from "../hooks/useRolePermissions.js";
import PageHeader from "./PageHeader.jsx";

const ENGINES_PATH = "/api/admin/runtime-engines";

const catalogConfig = {
  embedding: {
    apiPath: "/api/admin/embedding-models",
    title: "Model catalog",
    description: "Manage embedding models used for document vectorization and tutor knowledge retrieval.",
    singular: "embedding model",
    providerPlaceholder: "nomic-embed-text / text-embedding-3-small / bge-m3",
    suggestions: [
      { label: "Ollama: nomic-embed-text (768d)", model: "nomic-embed-text", dims: 768 },
      { label: "Ollama: bge-m3 (1024d)", model: "bge-m3", dims: 1024 },
      { label: "Ollama: mxbai-embed-large (1024d)", model: "mxbai-embed-large", dims: 1024 },
      { label: "OpenAI: text-embedding-3-small (1536d)", model: "text-embedding-3-small", dims: 1536 },
      { label: "OpenAI: text-embedding-3-large (3072d)", model: "text-embedding-3-large", dims: 3072 },
    ],
  },
  interaction: {
    apiPath: "/api/admin/interaction-models",
    title: "Interaction models",
    description: "Manage the language models that power active tutor conversations and benchmark generation.",
    singular: "interaction model",
    providerPlaceholder: "llama3.3:70b / gpt-4o / claude-3-5-sonnet-20241022",
    suggestions: [
      { label: "Ollama: llama3.3 (70B)", model: "llama3.3" },
      { label: "Ollama: qwen2.5 (32B/72B)", model: "qwen2.5:32b" },
      { label: "Ollama: deepseek-r1 (14B/32B)", model: "deepseek-r1:14b" },
      { label: "OpenAI: gpt-4o", model: "gpt-4o" },
      { label: "OpenAI: gpt-4o-mini", model: "gpt-4o-mini" },
      { label: "Anthropic: claude-3-5-sonnet-20241022", model: "claude-3-5-sonnet-20241022" },
      { label: "Anthropic: claude-3-5-haiku-20241022", model: "claude-3-5-haiku-20241022" },
    ],
  },
};

export default function ModelCatalogManager({ kind }) {
  const config = catalogConfig[kind];
  const isEmbedding = kind === "embedding";
  const { message } = App.useApp();
  const { canWrite, canDelete } = useRolePermissions();
  const [form] = Form.useForm();
  const [models, setModels] = useState([]);
  const [engines, setEngines] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");
  const [engineError, setEngineError] = useState("");
  const [open, setOpen] = useState(false);
  const [editingModel, setEditingModel] = useState(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [search, setSearch] = useState("");
  const [deletingId, setDeletingId] = useState("");

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

  const openCreate = () => {
    setEditingModel(null);
    form.resetFields();
    setOpen(true);
  };

  const openEdit = (model) => {
    setEditingModel(model);
    form.setFieldsValue({
      name: model.name,
      runtimeEngineId: model.runtimeEngineId,
      providerModelName: model.providerModelName ?? undefined,
      settingsJson: model.settingsJson ?? undefined,
      isEnabled: model.isEnabled,
      embeddingDimensions: model.embeddingDimensions,
    });
    setOpen(true);
  };

  const close = () => {
    if (saving) return;
    setOpen(false);
    setEditingModel(null);
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
      if (isEmbedding && !editingModel) payload.embeddingDimensions = Number(values.embeddingDimensions);
      if (editingModel) {
        const updated = await apiFetch(`${config.apiPath}/${editingModel.id}`, { method: "PUT", json: payload });
        setModels((current) => current.map((model) => model.id === editingModel.id ? (updated ?? { ...model, ...payload }) : model));
        message.success(`${config.singular} updated.`);
      } else {
        const created = await apiFetch(config.apiPath, { method: "POST", json: payload });
        setModels((current) => [created, ...current]);
        message.success(`${config.singular} created.`);
      }
      setOpen(false);
      setEditingModel(null);
      form.resetFields();
    } catch (requestError) {
      setFormError(getApiErrorDescription(requestError, `Could not ${editingModel ? "update" : "create"} the ${config.singular}.`));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (model) => {
    setDeletingId(model.id);
    try {
      await apiFetch(`${config.apiPath}/${model.id}`, { method: "DELETE", parse: "none" });
      setModels((current) => current.filter((item) => item.id !== model.id));
      message.success(`${config.singular} deleted.`);
    } catch (requestError) {
      message.error(getApiErrorDescription(requestError, `Could not delete the ${config.singular}.`));
    } finally {
      setDeletingId("");
    }
  };

  const columns = [
    { title: "Name", dataIndex: "name", sorter: (a, b) => a.name.localeCompare(b.name), render: (value, model) => <div className="management-name-cell"><Typography.Text strong>{value}</Typography.Text><Typography.Text type="secondary" ellipsis={{ tooltip: model.providerModelName }}>{model.providerModelName || "Provider model not specified"}</Typography.Text></div> },
    { title: "Runtime", dataIndex: "runtimeEngineName", width: 160, ellipsis: true, render: (value) => value || "—" },
    ...(isEmbedding ? [{ title: "Dimensions", dataIndex: "embeddingDimensions", width: 120, sorter: (a, b) => a.embeddingDimensions - b.embeddingDimensions, render: (value) => value > 0 ? Number(value).toLocaleString() : "—" }] : []),
    { title: "Status", dataIndex: "isEnabled", width: 100, filters: [{ text: "Enabled", value: true }, { text: "Disabled", value: false }], onFilter: (value, record) => record.isEnabled === value, render: (value) => <Tag color={value ? "success" : "default"}>{value ? "Enabled" : "Disabled"}</Tag> },
    ...(canWrite || canDelete ? [{
      title: "",
      key: "action",
      width: canDelete ? 90 : 50,
      align: "right",
      render: (_, model) => (
        <Space size={6}>
          {canWrite ? <Button aria-label={`Edit ${config.singular}`} title={`Edit ${config.singular}`} icon={<EditOutlined />} onClick={() => openEdit(model)} /> : null}
          {canDelete ? (
            <Popconfirm title={`Delete this ${config.singular}?`} description="Tutors depending on this model may stop working." okText="Delete" okButtonProps={{ danger: true }} onConfirm={() => remove(model)}>
              <Button danger aria-label={`Delete ${config.singular}`} title={`Delete ${config.singular}`} icon={<DeleteOutlined />} loading={deletingId === model.id} />
            </Popconfirm>
          ) : null}
        </Space>
      ),
    }] : []),
  ];

  return (
    <main className="workspace-page">
      <div className="workspace-page-inner">
        <PageHeader
          title={config.title}
          description={config.description}
          extra={[<Button key="refresh" icon={<ReloadOutlined />} onClick={() => load(true)} loading={refreshing}>Refresh</Button>, canWrite ? <Button key="add" type="primary" icon={<PlusOutlined />} onClick={openCreate}>Add model</Button> : null].filter(Boolean)}
        />
        {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}
        <Input className="management-search" prefix={<SearchOutlined />} allowClear value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search models" />
        <Table className="workspace-table" rowKey="id" columns={columns} dataSource={filteredModels} loading={loading} size="middle" pagination={{ pageSize: 10, showSizeChanger: true }} locale={{ emptyText: <Empty description={`No ${config.title.toLowerCase()} found`} /> }} />
      </div>

      <Drawer
        title={editingModel ? `Edit ${config.singular}` : `Add ${config.singular}`}
        open={open}
        onClose={close}
        size={560}
        destroyOnHidden
        footer={<Space style={{ width: "100%", justifyContent: "flex-end" }}><Button onClick={close} disabled={saving}>Cancel</Button><Button type="primary" onClick={() => form.submit()} loading={saving} disabled={!editingModel && !engines.length}>Save model</Button></Space>}
      >
        {formError ? <Alert type="error" title={formError} showIcon style={{ marginBottom: 16 }} /> : null}
        {engineError ? <Alert type="error" title={engineError} showIcon style={{ marginBottom: 16 }} /> : null}
        {!editingModel && !engines.length && !engineError ? <Alert type="warning" title="No enabled runtime engines are available. Add or enable a runtime engine first." showIcon style={{ marginBottom: 16 }} /> : null}
        <Form form={form} layout="vertical" onFinish={save} requiredMark={false} initialValues={{ isEnabled: true, embeddingDimensions: 0 }}>
          <Form.Item label="Name" name="name" rules={[{ required: true, whitespace: true, message: "Enter a model name." }]}><Input placeholder={`My ${isEmbedding ? "Embedding" : "Interaction"} Model`} /></Form.Item>
          <Form.Item label="Runtime engine" name="runtimeEngineId" rules={[{ required: true, message: "Select a runtime engine." }]}><Select placeholder="Select an enabled runtime engine" options={engines.map((engine) => ({ value: engine.id, label: engine.name }))} /></Form.Item>
          <Form.Item label="Provider model name" name="providerModelName" extra={(
            <Space wrap size={[4, 4]} style={{ marginTop: 6 }}>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>Suggestions:</Typography.Text>
              {config.suggestions.map((item) => (
                <Tag
                  key={item.model}
                  style={{ cursor: "pointer" }}
                  onClick={() => {
                    form.setFieldValue("providerModelName", item.model);
                    if (isEmbedding && !editingModel && item.dims) {
                      form.setFieldValue("embeddingDimensions", item.dims);
                    }
                  }}
                >
                  {item.label}
                </Tag>
              ))}
            </Space>
          )}><Input placeholder={config.providerPlaceholder} /></Form.Item>
          {isEmbedding ? (
            <Form.Item label="Embedding dimensions" name="embeddingDimensions" extra={editingModel ? "Dimensions cannot be changed after a model is created." : undefined}>
              <InputNumber min={0} precision={0} style={{ width: "100%" }} disabled={Boolean(editingModel)} />
            </Form.Item>
          ) : null}
          <Form.Item label="Settings JSON" name="settingsJson"><Input.TextArea className="workspace-code" autoSize={{ minRows: 4, maxRows: 12 }} placeholder='{"temperature": 0.7}' /></Form.Item>
          <Form.Item label="Enabled" name="isEnabled" valuePropName="checked"><Switch /></Form.Item>
        </Form>
      </Drawer>
    </main>
  );
}
