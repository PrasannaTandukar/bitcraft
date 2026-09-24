import { CheckCircleOutlined, CloseCircleOutlined, DeleteOutlined, EditOutlined, ExperimentOutlined, PlusOutlined, ReloadOutlined, SearchOutlined } from "@ant-design/icons";
import { Alert, Button, Drawer, Empty, Flex, Form, Input, Popconfirm, Select, Space, Switch, Table, Tag, Typography } from "antd";
import PageHeader from "../../components/PageHeader.jsx";
import useRolePermissions from "../../hooks/useRolePermissions.js";
import useRuntimeEngines from "../../features/admin/hooks/useRuntimeEngines.js";
import { PROVIDER_LABELS, PROVIDER_TYPES } from "../../features/admin/utils/runtimeEngineUtils.js";

function buildColumns({ canWrite, canDelete, deletingId, openEdit, remove }) {
  return [
    {
      title: "Runtime",
      key: "runtime",
      sorter: (a, b) => a.name.localeCompare(b.name),
      render: (_, engine) => (
        <div className="management-name-cell">
          <Typography.Text strong>{engine.name}</Typography.Text>
          <Typography.Text type="secondary" ellipsis={{ tooltip: engine.endpoint }}>
            {engine.endpoint || "Endpoint not specified"}
          </Typography.Text>
        </div>
      ),
    },
    {
      title: "Provider",
      dataIndex: "providerType",
      width: 150,
      filters: PROVIDER_TYPES.map((provider) => ({ text: provider.label, value: provider.value })),
      onFilter: (value, record) => record.providerType === value,
      render: (value) => PROVIDER_LABELS[value] ?? value,
    },
    {
      title: "Credential",
      dataIndex: "hasApiKeySecret",
      width: 140,
      render: (value) => (value ? <Tag color="blue">Configured</Tag> : <Typography.Text type="secondary">None</Typography.Text>),
    },
    {
      title: "Status",
      dataIndex: "isEnabled",
      width: 100,
      filters: [{ text: "Enabled", value: true }, { text: "Disabled", value: false }],
      onFilter: (value, record) => record.isEnabled === value,
      render: (value) => <Tag color={value ? "success" : "default"}>{value ? "Enabled" : "Disabled"}</Tag>,
    },
    ...(canWrite || canDelete
      ? [{
          title: "",
          key: "action",
          width: canDelete ? 90 : 50,
          align: "right",
          render: (_, engine) => (
            <Space size={6}>
              {canWrite ? (
                <Button aria-label="Edit engine" title="Edit engine" icon={<EditOutlined />} onClick={() => openEdit(engine)} />
              ) : null}
              {canDelete ? (
                <Popconfirm
                  title="Delete this runtime engine?"
                  description="Models depending on this engine may stop working."
                  okText="Delete"
                  okButtonProps={{ danger: true }}
                  onConfirm={() => remove(engine)}
                >
                  <Button danger aria-label="Delete engine" title="Delete engine" icon={<DeleteOutlined />} loading={deletingId === engine.id} />
                </Popconfirm>
              ) : null}
            </Space>
          ),
        }]
      : []),
  ];
}

export default function RuntimeEnginesPage() {
  const [form] = Form.useForm();
  const { canWrite, canDelete } = useRolePermissions();
  const {
    loading,
    refreshing,
    error,
    open,
    editingEngine,
    saving,
    formError,
    search,
    setSearch,
    deletingId,
    testing,
    testResult,
    setTestResult,
    filteredEngines,
    load,
    openCreate,
    openEdit,
    handleProviderTypeChange,
    testConnection,
    close,
    save,
    remove,
  } = useRuntimeEngines(form);

  const columns = buildColumns({ canWrite, canDelete, deletingId, openEdit, remove });

  return (
    <main className="workspace-page">
      <div className="workspace-page-inner">
        <PageHeader
          title="Runtime engines"
          description="Manage the provider endpoints used by interaction and embedding models."
          extra={[
            <Button key="refresh" icon={<ReloadOutlined />} onClick={() => load(true)} loading={refreshing}>Refresh</Button>,
            canWrite ? <Button key="add" type="primary" icon={<PlusOutlined />} onClick={openCreate}>Add engine</Button> : null,
          ].filter(Boolean)}
        />
        {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}
        <Input className="management-search" prefix={<SearchOutlined />} allowClear value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search engines" />
        <Table
          className="workspace-table"
          rowKey="id"
          columns={columns}
          dataSource={filteredEngines}
          loading={loading}
          size="middle"
          pagination={{ pageSize: 10, showSizeChanger: true }}
          locale={{ emptyText: <Empty description="No runtime engines found" /> }}
        />
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
          <Form.Item label="Name" name="name" rules={[{ required: true, whitespace: true, message: "Enter an engine name." }]}>
            <Input placeholder="Local Ollama or Llama.CPP" />
          </Form.Item>
          <Form.Item label="Provider type" name="providerType">
            <Select options={PROVIDER_TYPES} onChange={handleProviderTypeChange} />
          </Form.Item>
          <Form.Item
            label="Endpoint"
            name="endpoint"
            rules={[{ required: true, whitespace: true, message: "Enter the runtime endpoint." }]}
            extra="OpenAI Compatible servers default to port 8080 (e.g. http://localhost:8080/v1), Ollama to 11434 (e.g. http://localhost:11434)."
          >
            <Input placeholder="http://localhost:8080/v1" />
          </Form.Item>
          <Form.Item
            label="API key secret reference"
            name="apiKeySecretRef"
            extra="Store only a secret reference here, never an API key value. Optional for local Llama.CPP/Ollama."
          >
            <Input placeholder="my-api-key-ref" />
          </Form.Item>
          <Form.Item label="Enabled" name="isEnabled" valuePropName="checked"><Switch /></Form.Item>
        </Form>
      </Drawer>
    </main>
  );
}
