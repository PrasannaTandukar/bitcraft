import { CheckCircleOutlined, CloseCircleOutlined, DeleteOutlined, EditOutlined, ExperimentOutlined, PlusOutlined, ReloadOutlined, SearchOutlined } from "@ant-design/icons";
import { Alert, App, Button, Drawer, Empty, Flex, Form, Input, Popconfirm, Select, Space, Switch, Table, Tag, Typography } from "antd";
import { useCallback, useEffect, useMemo, useState } from "react";
import { apiFetch, getApiErrorDescription } from "./api/client.js";
import PageHeader from "./components/PageHeader.jsx";
import useRolePermissions from "./hooks/useRolePermissions.js";

const API_PATH = "/api/admin/runtime-engines";
const PROVIDER_TYPES = [
  { value: 0, label: "Ollama", defaultEndpoint: "http://localhost:11434" },
  { value: 1, label: "OpenAI", defaultEndpoint: "https://api.openai.com/v1" },
  { value: 101, label: "Anthropic", defaultEndpoint: "https://api.anthropic.com" },
];
const PROVIDER_LABELS = Object.fromEntries(PROVIDER_TYPES.map((provider) => [provider.value, provider.label]));

export default function RuntimeEnginesPage() {
  const { message } = App.useApp();
  const { canWrite, canDelete } = useRolePermissions();
  const [form] = Form.useForm();
  const [engines, setEngines] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");
  const [open, setOpen] = useState(false);
  const [editingEngine, setEditingEngine] = useState(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [search, setSearch] = useState("");
  const [deletingId, setDeletingId] = useState("");
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState(null);

  const load = useCallback((refresh = false) => {
    if (refresh) setRefreshing(true); else setLoading(true);
    apiFetch(API_PATH)
      .then((data) => {
        setEngines(data ?? []);
        setError("");
      })
      .catch((requestError) => setError(getApiErrorDescription(requestError, "Could not load runtime engines.")))
      .finally(() => {
        setLoading(false);
        setRefreshing(false);
      });
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => load(), 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  const filteredEngines = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!query) return engines;
    return engines.filter((engine) => [engine.name, engine.endpoint, PROVIDER_LABELS[engine.providerType]].some((value) => value?.toLowerCase().includes(query)));
  }, [engines, search]);

  const openCreate = () => {
    setEditingEngine(null);
    setTestResult(null);
    form.resetFields();
    setOpen(true);
  };

  const openEdit = (engine) => {
    setEditingEngine(engine);
    setTestResult(null);
    form.setFieldsValue({
      name: engine.name,
      providerType: engine.providerType,
      endpoint: engine.endpoint,
      apiKeySecretRef: engine.apiKeySecretRef ?? undefined,
      isEnabled: engine.isEnabled,
    });
    setOpen(true);
  };

  const handleProviderTypeChange = (value) => {
    const currentEndpoint = form.getFieldValue("endpoint");
    const matched = PROVIDER_TYPES.find((p) => p.value === value);
    if (matched && (!currentEndpoint || PROVIDER_TYPES.some((p) => p.defaultEndpoint === currentEndpoint))) {
      form.setFieldValue("endpoint", matched.defaultEndpoint);
    }
  };

  const testConnection = async () => {
    const values = form.getFieldsValue();
    if (!values.endpoint?.trim()) {
      message.error("Enter an endpoint to test.");
      return;
    }
    setTesting(true);
    setTestResult(null);
    try {
      const result = await apiFetch(`${API_PATH}/test`, {
        method: "POST",
        json: {
          providerType: Number(values.providerType ?? 0),
          endpoint: values.endpoint.trim(),
          apiKeySecretRef: values.apiKeySecretRef?.trim() || null,
        },
      });
      setTestResult(result);
    } catch (requestError) {
      setTestResult({
        isSuccess: false,
        latencyMs: 0,
        message: getApiErrorDescription(requestError, "Test connection request failed."),
      });
    } finally {
      setTesting(false);
    }
  };

  const close = () => {
    if (saving || testing) return;
    setOpen(false);
    setEditingEngine(null);
    setFormError("");
    setTestResult(null);
    form.resetFields();
  };

  const save = async (values) => {
    setSaving(true);
    setFormError("");
    const payload = {
      name: values.name.trim(),
      providerType: Number(values.providerType),
      endpoint: values.endpoint.trim(),
      apiKeySecretRef: values.apiKeySecretRef?.trim() || null,
      isEnabled: values.isEnabled,
    };
    try {
      if (editingEngine) {
        const updated = await apiFetch(`${API_PATH}/${editingEngine.id}`, { method: "PUT", json: payload });
        setEngines((current) => current.map((engine) => engine.id === editingEngine.id ? (updated ?? { ...engine, ...payload }) : engine));
        message.success("Runtime engine updated.");
      } else {
        const created = await apiFetch(API_PATH, { method: "POST", json: payload });
        setEngines((current) => [created, ...current]);
        message.success("Runtime engine created.");
      }
      setOpen(false);
      setEditingEngine(null);
      form.resetFields();
    } catch (requestError) {
      setFormError(getApiErrorDescription(requestError, `Could not ${editingEngine ? "update" : "create"} the runtime engine.`));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (engine) => {
    setDeletingId(engine.id);
    try {
      await apiFetch(`${API_PATH}/${engine.id}`, { method: "DELETE", parse: "none" });
      setEngines((current) => current.filter((item) => item.id !== engine.id));
      message.success("Runtime engine deleted.");
    } catch (requestError) {
      message.error(getApiErrorDescription(requestError, "Could not delete the runtime engine."));
    } finally {
      setDeletingId("");
    }
  };

  const columns = [
    { title: "Runtime", key: "runtime", sorter: (a, b) => a.name.localeCompare(b.name), render: (_, engine) => <div className="management-name-cell"><Typography.Text strong>{engine.name}</Typography.Text><Typography.Text type="secondary" ellipsis={{ tooltip: engine.endpoint }}>{engine.endpoint || "Endpoint not specified"}</Typography.Text></div> },
    { title: "Provider", dataIndex: "providerType", width: 150, filters: PROVIDER_TYPES.map((provider) => ({ text: provider.label, value: provider.value })), onFilter: (value, record) => record.providerType === value, render: (value) => PROVIDER_LABELS[value] ?? value },
    { title: "Credential", dataIndex: "hasApiKeySecret", width: 140, render: (value) => value ? <Tag color="blue">Configured</Tag> : <Typography.Text type="secondary">None</Typography.Text> },
    { title: "Status", dataIndex: "isEnabled", width: 100, filters: [{ text: "Enabled", value: true }, { text: "Disabled", value: false }], onFilter: (value, record) => record.isEnabled === value, render: (value) => <Tag color={value ? "success" : "default"}>{value ? "Enabled" : "Disabled"}</Tag> },
    ...(canWrite || canDelete ? [{
      title: "",
      key: "action",
      width: canDelete ? 90 : 50,
      align: "right",
      render: (_, engine) => (
        <Space size={6}>
          {canWrite ? <Button aria-label="Edit engine" title="Edit engine" icon={<EditOutlined />} onClick={() => openEdit(engine)} /> : null}
          {canDelete ? (
            <Popconfirm title="Delete this runtime engine?" description="Models depending on this engine may stop working." okText="Delete" okButtonProps={{ danger: true }} onConfirm={() => remove(engine)}>
              <Button danger aria-label="Delete engine" title="Delete engine" icon={<DeleteOutlined />} loading={deletingId === engine.id} />
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
          title="Runtime engines"
          description="Manage the provider endpoints used by interaction and embedding models."
          extra={[<Button key="refresh" icon={<ReloadOutlined />} onClick={() => load(true)} loading={refreshing}>Refresh</Button>, canWrite ? <Button key="add" type="primary" icon={<PlusOutlined />} onClick={openCreate}>Add engine</Button> : null].filter(Boolean)}
        />
        {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}
        <Input className="management-search" prefix={<SearchOutlined />} allowClear value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search engines" />
        <Table className="workspace-table" rowKey="id" columns={columns} dataSource={filteredEngines} loading={loading} size="middle" pagination={{ pageSize: 10, showSizeChanger: true }} locale={{ emptyText: <Empty description="No runtime engines found" /> }} />
      </div>

      <Drawer
        title={editingEngine ? "Edit runtime engine" : "Add runtime engine"}
        open={open}
        onClose={close}
        size={560}
        destroyOnHidden
        footer={
          <Space style={{ width: "100%", justifyContent: "flex-end" }}>
            <Button onClick={close} disabled={saving || testing}>Cancel</Button>
            <Button type="default" htmlType="button" icon={<ExperimentOutlined />} onClick={testConnection} loading={testing} disabled={saving}>Test connection</Button>
            <Button type="primary" htmlType="button" onClick={() => form.submit()} loading={saving} disabled={testing}>Save engine</Button>
          </Space>
        }
      >
        {formError ? <Alert type="error" title={formError} showIcon style={{ marginBottom: 16 }} /> : null}
        {testResult ? (
          <Alert
            type={testResult.isSuccess ? "success" : "warning"}
            icon={testResult.isSuccess ? <CheckCircleOutlined /> : <CloseCircleOutlined />}
            title={testResult.isSuccess ? `Connection verified (${testResult.latencyMs}ms)` : "Connection test failed"}
            description={
              <div>
                <div>{testResult.message}</div>
                {testResult.availableModels && testResult.availableModels.length > 0 ? (
                  <div style={{ marginTop: 8 }}>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>Available models ({testResult.availableModels.length}):</Typography.Text>
                    <Flex gap={4} wrap style={{ marginTop: 4 }}>
                      {testResult.availableModels.map((m) => (
                        <Tag key={m}>{m}</Tag>
                      ))}
                    </Flex>
                  </div>
                ) : null}
              </div>
            }
            showIcon
            closable
            onClose={() => setTestResult(null)}
            style={{ marginBottom: 16 }}
          />
        ) : null}
        <Form form={form} layout="vertical" onFinish={save} requiredMark={false} initialValues={{ providerType: 0, endpoint: "http://localhost:11434", isEnabled: true }}>
          <Form.Item label="Name" name="name" rules={[{ required: true, whitespace: true, message: "Enter an engine name." }]}><Input placeholder="Local Ollama or Llama.CPP" /></Form.Item>
          <Form.Item label="Provider type" name="providerType"><Select options={PROVIDER_TYPES} onChange={handleProviderTypeChange} /></Form.Item>
          <Form.Item label="Endpoint" name="endpoint" rules={[{ required: true, whitespace: true, message: "Enter the runtime endpoint." }]} extra="OpenAI Compatible servers default to port 8080 (e.g. http://localhost:8080/v1), Ollama to 11434 (e.g. http://localhost:11434)."><Input placeholder="http://localhost:8080/v1" /></Form.Item>
          <Form.Item label="API key secret reference" name="apiKeySecretRef" extra="Store only a secret reference here, never an API key value. Optional for local Llama.CPP/Ollama."><Input placeholder="my-api-key-ref" /></Form.Item>
          <Form.Item label="Enabled" name="isEnabled" valuePropName="checked"><Switch /></Form.Item>
        </Form>
      </Drawer>
    </main>
  );
}
