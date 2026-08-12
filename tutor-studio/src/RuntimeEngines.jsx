import { PlusOutlined, ReloadOutlined, SearchOutlined } from "@ant-design/icons";
import { Alert, Button, Drawer, Empty, Form, Input, Select, Space, Switch, Table, Tag, Typography } from "antd";
import { useCallback, useEffect, useMemo, useState } from "react";
import { apiFetch, getApiErrorDescription } from "./api/client.js";
import PageHeader from "./components/PageHeader.jsx";

const API_PATH = "/api/admin/runtime-engines";
const PROVIDER_TYPES = [
  { value: 0, label: "Ollama" },
  { value: 1, label: "OpenAI" },
  { value: 2, label: "Anthropic" },
];
const PROVIDER_LABELS = Object.fromEntries(PROVIDER_TYPES.map((provider) => [provider.value, provider.label]));

export default function RuntimeEnginesPage() {
  const [form] = Form.useForm();
  const [engines, setEngines] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [search, setSearch] = useState("");

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
      const created = await apiFetch(API_PATH, {
        method: "POST",
        json: {
          name: values.name.trim(),
          providerType: Number(values.providerType),
          endpoint: values.endpoint.trim(),
          apiKeySecretRef: values.apiKeySecretRef?.trim() || null,
          isEnabled: values.isEnabled,
        },
      });
      setEngines((current) => [created, ...current]);
      setOpen(false);
      form.resetFields();
    } catch (requestError) {
      setFormError(getApiErrorDescription(requestError, "Could not create the runtime engine."));
    } finally {
      setSaving(false);
    }
  };

  const columns = [
    { title: "Runtime", key: "runtime", sorter: (a, b) => a.name.localeCompare(b.name), render: (_, engine) => <div className="management-name-cell"><Typography.Text strong>{engine.name}</Typography.Text><Typography.Text type="secondary" ellipsis={{ tooltip: engine.endpoint }}>{engine.endpoint || "Endpoint not specified"}</Typography.Text></div> },
    { title: "Provider", dataIndex: "providerType", width: 140, filters: PROVIDER_TYPES.map((provider) => ({ text: provider.label, value: provider.value })), onFilter: (value, record) => record.providerType === value, render: (value) => PROVIDER_LABELS[value] ?? value },
    { title: "Credential", dataIndex: "hasApiKeySecret", width: 130, render: (value) => value ? <Tag color="blue">Secret configured</Tag> : <Typography.Text type="secondary">None</Typography.Text> },
    { title: "Status", dataIndex: "isEnabled", width: 110, filters: [{ text: "Enabled", value: true }, { text: "Disabled", value: false }], onFilter: (value, record) => record.isEnabled === value, render: (value) => <Tag color={value ? "success" : "default"}>{value ? "Enabled" : "Disabled"}</Tag> },
  ];

  return (
    <main className="workspace-page">
      <div className="workspace-page-inner">
        <PageHeader
          title="Runtime engines"
          description="Manage the provider endpoints used by interaction and embedding models."
          extra={[<Button key="refresh" icon={<ReloadOutlined />} onClick={() => load(true)} loading={refreshing}>Refresh</Button>, <Button key="add" type="primary" icon={<PlusOutlined />} onClick={() => setOpen(true)}>Add engine</Button>]}
        />
        {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}
        <Input className="management-search" prefix={<SearchOutlined />} allowClear value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search engines" />
        <Table className="workspace-table" rowKey="id" columns={columns} dataSource={filteredEngines} loading={loading} scroll={{ x: 760 }} pagination={{ pageSize: 10, showSizeChanger: true }} locale={{ emptyText: <Empty description="No runtime engines found" /> }} />
      </div>

      <Drawer title="Add runtime engine" open={open} onClose={close} size={560} destroyOnHidden footer={<Space style={{ width: "100%", justifyContent: "flex-end" }}><Button onClick={close} disabled={saving}>Cancel</Button><Button type="primary" onClick={() => form.submit()} loading={saving}>Save engine</Button></Space>}>
        {formError ? <Alert type="error" title={formError} showIcon style={{ marginBottom: 16 }} /> : null}
        <Form form={form} layout="vertical" onFinish={save} requiredMark={false} initialValues={{ providerType: 0, isEnabled: true }}>
          <Form.Item label="Name" name="name" rules={[{ required: true, whitespace: true, message: "Enter an engine name." }]}><Input placeholder="Local Ollama" /></Form.Item>
          <Form.Item label="Provider type" name="providerType"><Select options={PROVIDER_TYPES} /></Form.Item>
          <Form.Item label="Endpoint" name="endpoint" rules={[{ required: true, whitespace: true, message: "Enter the runtime endpoint." }]}><Input placeholder="http://localhost:11434" /></Form.Item>
          <Form.Item label="API key secret reference" name="apiKeySecretRef" extra="Store only a secret reference here, never an API key value."><Input placeholder="my-api-key-ref" /></Form.Item>
          <Form.Item label="Enabled" name="isEnabled" valuePropName="checked"><Switch /></Form.Item>
        </Form>
      </Drawer>
    </main>
  );
}
