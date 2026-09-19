import { DeleteOutlined, EditOutlined, ExperimentOutlined, PlusOutlined, ReloadOutlined, SearchOutlined } from "@ant-design/icons";
import { Alert, App, Button, Drawer, Empty, Flex, Form, Input, InputNumber, Popconfirm, Select, Space, Switch, Table, Tag, Typography } from "antd";
import { useCallback, useEffect, useMemo, useState } from "react";
import { apiFetch, getApiErrorDescription } from "../api/client.js";
import useRolePermissions from "../hooks/useRolePermissions.js";
import PageHeader from "./PageHeader.jsx";
import ModelImageInputTest from "./ModelImageInputTest.jsx";

const ENGINES_PATH = "/api/admin/runtime-engines";

// Embedding dimension reference heuristics for popular embedding models
const EMBEDDING_DIM_MAP = {
  "nomic-embed-text": 768,
  "bge-m3": 1024,
  "bge-small": 384,
  "bge-large": 1024,
  "mxbai-embed-large": 1024,
  "all-minilm": 384,
  "all-mpnet-base-v2": 768,
  "text-embedding-3-small": 1536,
  "text-embedding-3-large": 3072,
  "text-embedding-ada-002": 1536,
  "embed-english-v3.0": 1024,
  "embed-multilingual-v3.0": 1024,
};

// Priority keywords / popular model tokens to rank top popular models first
const POPULAR_EMBEDDING_PRIORITY = [
  "text-embedding-3-small",
  "text-embedding-3-large",
  "text-embedding-ada-002",
  "nomic-embed-text",
  "bge-m3",
  "bge-large",
  "bge-small",
  "mxbai-embed-large",
  "all-minilm",
  "all-mpnet-base-v2",
];

const POPULAR_INTERACTION_PRIORITY = [
  "gpt-4o",
  "gpt-4o-mini",
  "gpt-4-turbo",
  "gpt-4",
  "gpt-3.5-turbo",
  "claude-3-5-sonnet",
  "claude-3-5-haiku",
  "claude-3-opus",
  "llama3.3",
  "llama3.2",
  "llama3.1",
  "llama3",
  "qwen2.5",
  "deepseek-r1",
  "deepseek-v3",
  "mistral",
  "mixtral",
  "phi4",
  "phi3",
  "gemma2",
];

function scoreModelPopularity(modelName, isEmbedding) {
  const lower = modelName.toLowerCase();
  const priorityList = isEmbedding ? POPULAR_EMBEDDING_PRIORITY : POPULAR_INTERACTION_PRIORITY;
  const matchIndex = priorityList.findIndex((p) => lower.includes(p));
  if (matchIndex !== -1) {
    return matchIndex;
  }
  return 1000;
}

function filterAndSortAvailableModels(rawModels, isEmbedding, limit = 10) {
  if (!rawModels || !rawModels.length) return [];

  // Clean duplicates and trim
  const unique = Array.from(new Set(rawModels.map((m) => m?.trim()).filter(Boolean)));

  // Categorize or sort by popularity
  const sorted = [...unique].sort((a, b) => {
    const scoreA = scoreModelPopularity(a, isEmbedding);
    const scoreB = scoreModelPopularity(b, isEmbedding);
    if (scoreA !== scoreB) {
      return scoreA - scoreB;
    }
    return a.localeCompare(b);
  });

  return sorted.slice(0, limit);
}

const catalogConfig = {
  embedding: {
    apiPath: "/api/admin/embedding-models",
    title: "Model catalog",
    description: "Manage embedding models used for document vectorization and tutor knowledge retrieval.",
    singular: "embedding model",
  },
  interaction: {
    apiPath: "/api/admin/interaction-models",
    title: "Interaction models",
    description: "Manage the language models that power active tutor conversations and benchmark generation.",
    singular: "interaction model",
  },
};

export default function ModelCatalogManager({ kind }) {
  const config = catalogConfig[kind];
  const isEmbedding = kind === "embedding";
  const { message } = App.useApp();
  const { canWrite, canDelete } = useRolePermissions();
  const [form] = Form.useForm();
  const selectedEngineId = Form.useWatch("runtimeEngineId", form);
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

  // Engine model discovery state
  const [discoveredModelsByEngine, setDiscoveredModelsByEngine] = useState({});
  const [fetchingEngineModels, setFetchingEngineModels] = useState(false);
  const [engineProbeError, setEngineProbeError] = useState("");

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

  const loadEngineModels = useCallback(async (engineId, force = false) => {
    if (!engineId) return;
    if (!force && discoveredModelsByEngine[engineId] !== undefined) {
      return;
    }

    const engine = engines.find((e) => e.id === engineId);
    if (!engine || !engine.endpoint) return;

    setFetchingEngineModels(true);
    setEngineProbeError("");
    try {
      const result = await apiFetch(`${ENGINES_PATH}/test`, {
        method: "POST",
        json: {
          providerType: Number(engine.providerType ?? 0),
          endpoint: engine.endpoint.trim(),
          apiKeySecretRef: null,
          runtimeEngineId: engine.id,
        },
      });

      // The probe returns HTTP 200 with isSuccess=false on failure; surface the backend
      // diagnostic instead of a generic message so the admin can tell auth, provider-type,
      // and reachability problems apart.
      if (result && result.isSuccess === false) {
        setEngineProbeError(result.message || "The endpoint was unreachable or returned no model catalog.");
        setDiscoveredModelsByEngine((prev) => ({
          ...prev,
          [engineId]: [],
        }));
        return;
      }

      const rawModels = result?.availableModels ?? [];
      if (rawModels.length === 0 && result?.message) {
        setEngineProbeError(result.message);
      }
      const topModels = filterAndSortAvailableModels(rawModels, isEmbedding, 10);
      setDiscoveredModelsByEngine((prev) => ({
        ...prev,
        [engineId]: topModels,
      }));
    } catch (requestError) {
      setEngineProbeError(getApiErrorDescription(requestError, "Could not query available models from engine."));
      setDiscoveredModelsByEngine((prev) => ({
        ...prev,
        [engineId]: [],
      }));
    } finally {
      setFetchingEngineModels(false);
    }
  }, [discoveredModelsByEngine, engines, isEmbedding]);

  useEffect(() => {
    if (open && selectedEngineId) {
      const timer = window.setTimeout(() => {
        loadEngineModels(selectedEngineId);
      }, 0);
      return () => window.clearTimeout(timer);
    }
  }, [open, selectedEngineId, loadEngineModels]);

  const filteredModels = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!query) return models;
    return models.filter((model) => [model.name, model.providerModelName, model.runtimeEngineName].some((value) => value?.toLowerCase().includes(query)));
  }, [models, search]);

  const openCreate = () => {
    setEditingModel(null);
    setEngineProbeError("");
    form.resetFields();
    if (engines.length > 0) {
      form.setFieldValue("runtimeEngineId", engines[0].id);
    }
    setOpen(true);
  };

  const openEdit = (model) => {
    setEditingModel(model);
    setEngineProbeError("");
    form.setFieldsValue({
      name: model.name,
      runtimeEngineId: model.runtimeEngineId,
      providerModelName: model.providerModelName ?? undefined,
      settingsJson: model.settingsJson ?? undefined,
      isEnabled: model.isEnabled,
      supportsImageInput: model.supportsImageInput ?? false,
      embeddingDimensions: model.embeddingDimensions,
    });
    setOpen(true);
  };

  const close = () => {
    if (saving) return;
    setOpen(false);
    setEditingModel(null);
    setFormError("");
    setEngineProbeError("");
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
      if (!isEmbedding) payload.supportsImageInput = Boolean(values.supportsImageInput);
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
    { title: "Name", dataIndex: "name", sorter: (a, b) => a.name.localeCompare(b.name), render: (value, model) => <div className="management-name-cell"><Typography.Text strong>{value} {!isEmbedding && model.supportsImageInput ? <Tag color="purple">Vision</Tag> : null}</Typography.Text><Typography.Text type="secondary" ellipsis={{ tooltip: model.providerModelName }}>{model.providerModelName || "Provider model not specified"}</Typography.Text></div> },
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
        <Form form={form} layout="vertical" onFinish={save} requiredMark={false} initialValues={{ isEnabled: true, supportsImageInput: false, embeddingDimensions: 0 }}>
          <Form.Item label="Name" name="name" rules={[{ required: true, whitespace: true, message: "Enter a model name." }]}><Input placeholder="e.g. Local Llama 3.3 70B" /></Form.Item>
          <Form.Item label="Runtime engine" name="runtimeEngineId" rules={[{ required: true, message: "Select a runtime engine." }]}>
            <Select
              placeholder="Select an enabled runtime engine"
              options={engines.map((engine) => ({ value: engine.id, label: engine.name }))}
              onChange={(value) => loadEngineModels(value, true)}
            />
          </Form.Item>
          <Form.Item
            label="Provider model name"
            name="providerModelName"
            extra={(
              <div style={{ marginTop: 6 }}>
                <Flex align="center" justify="space-between" gap={8} style={{ marginBottom: 4 }}>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {fetchingEngineModels
                      ? "Fetching available models from provider…"
                      : (selectedEngineId && (discoveredModelsByEngine[selectedEngineId]?.length > 0))
                        ? `Available models on provider (top ${discoveredModelsByEngine[selectedEngineId].length}):`
                        : "Available models on provider:"}
                  </Typography.Text>
                  {selectedEngineId ? (
                    <Button
                      type="link"
                      size="small"
                      icon={<ReloadOutlined />}
                      loading={fetchingEngineModels}
                      onClick={() => loadEngineModels(selectedEngineId, true)}
                      style={{ padding: 0, height: "auto", fontSize: 12 }}
                    >
                      Refresh list
                    </Button>
                  ) : null}
                </Flex>

                {engineProbeError ? (
                  <Typography.Text type="warning" style={{ fontSize: 12, display: "block", marginBottom: 4 }}>
                    {engineProbeError}
                  </Typography.Text>
                ) : null}

                {selectedEngineId && (discoveredModelsByEngine[selectedEngineId]?.length > 0) ? (
                  <Flex wrap="wrap" gap={4} style={{ marginTop: 4 }}>
                    {discoveredModelsByEngine[selectedEngineId].map((modelName) => {
                      const matchedDim = isEmbedding
                        ? (EMBEDDING_DIM_MAP[modelName] ||
                          Object.entries(EMBEDDING_DIM_MAP).find(([k]) => modelName.toLowerCase().includes(k))?.[1])
                        : undefined;

                      return (
                        <Tag
                          key={modelName}
                          color="processing"
                          style={{ cursor: "pointer", margin: "2px 0" }}
                          onClick={() => {
                            form.setFieldValue("providerModelName", modelName);
                            if (isEmbedding && !editingModel && matchedDim) {
                              form.setFieldValue("embeddingDimensions", matchedDim);
                            }
                          }}
                        >
                          {modelName}{matchedDim ? ` (${matchedDim}d)` : ""}
                        </Tag>
                      );
                    })}
                  </Flex>
                ) : !fetchingEngineModels ? (
                  <Typography.Text type={engineProbeError ? "warning" : "secondary"} style={{ fontSize: 12 }}>
                    {selectedEngineId
                      ? (engineProbeError || "No models reported by provider or endpoint is currently offline.")
                      : "Select a runtime engine above to load available provider models."}
                  </Typography.Text>
                ) : null}
              </div>
            )}
          >
            <Input placeholder={discoveredModelsByEngine[selectedEngineId]?.length > 0 ? discoveredModelsByEngine[selectedEngineId][0] : "Provider model name (pick from detected list above)"} />
          </Form.Item>
          {isEmbedding ? (
            <Form.Item label="Embedding dimensions" name="embeddingDimensions" extra={editingModel ? "Dimensions cannot be changed after a model is created." : undefined}>
              <InputNumber min={0} precision={0} style={{ width: "100%" }} disabled={Boolean(editingModel)} />
            </Form.Item>
          ) : null}
          {!isEmbedding ? (
            <section aria-label="Capabilities">
              <Typography.Title level={5}>Capabilities</Typography.Title>
              <Form.Item label="Image input / Vision" name="supportsImageInput" valuePropName="checked" extra="Allows tutors using this model to receive images. Activate a new tutor revision to apply capability changes.">
                <Switch />
              </Form.Item>
              {editingModel ? <ModelImageInputTest key={editingModel.id} modelId={editingModel.id} disabled={saving} /> : null}
            </section>
          ) : null}
          <Form.Item label="Settings JSON" name="settingsJson"><Input.TextArea className="workspace-code" autoSize={{ minRows: 4, maxRows: 12 }} placeholder='{"temperature": 0.7}' /></Form.Item>
          <Form.Item label="Enabled" name="isEnabled" valuePropName="checked"><Switch /></Form.Item>
        </Form>
      </Drawer>
    </main>
  );
}
