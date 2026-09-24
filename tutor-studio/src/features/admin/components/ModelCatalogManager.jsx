import { DeleteOutlined, EditOutlined, PlusOutlined, ReloadOutlined, SearchOutlined } from "@ant-design/icons";
import { Alert, Button, Drawer, Empty, Flex, Form, Input, InputNumber, Popconfirm, Select, Space, Switch, Table, Tag, Typography } from "antd";
import { useEffect } from "react";
import useRolePermissions from "../../../hooks/useRolePermissions.js";
import PageHeader from "../../../components/PageHeader.jsx";
import ModelImageInputTest from "../../../components/ModelImageInputTest.jsx";
import useModelCatalog from "../hooks/useModelCatalog.js";
import { matchEmbeddingDimension } from "../utils/modelCatalogUtils.js";
function buildColumns({ config, isEmbedding, canWrite, canDelete, deletingId, openEdit, remove }) {
  return [
    {
      title: "Name",
      dataIndex: "name",
      sorter: (a, b) => a.name.localeCompare(b.name),
      render: (value, model) => (
        <div className="management-name-cell">
          <Typography.Text strong>
            {value}
            {!isEmbedding && model.supportsImageInput ? <Tag color="purple">Vision</Tag> : null}
            {!isEmbedding && model.supportsFunctionCalling ? <Tag color="blue">Tools</Tag> : null}
            {!isEmbedding && model.supportsStructuredOutput ? <Tag color="cyan">Structured</Tag> : null}
          </Typography.Text>
          <Typography.Text type="secondary" ellipsis={{ tooltip: model.providerModelName }}>
            {model.providerModelName || "Provider model not specified"}
          </Typography.Text>
        </div>
      ),
    },
    { title: "Runtime", dataIndex: "runtimeEngineName", width: 160, ellipsis: true, render: (value) => value || "—" },
    ...(isEmbedding
      ? [{
          title: "Dimensions",
          dataIndex: "embeddingDimensions",
          width: 120,
          sorter: (a, b) => a.embeddingDimensions - b.embeddingDimensions,
          render: (value) => (value > 0 ? Number(value).toLocaleString() : "—"),
        }]
      : []),
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
          render: (_, model) => (
            <Space size={6}>
              {canWrite ? (
                <Button aria-label={`Edit ${config.singular}`} title={`Edit ${config.singular}`} icon={<EditOutlined />} onClick={() => openEdit(model)} />
              ) : null}
              {canDelete ? (
                <Popconfirm
                  title={`Delete this ${config.singular}?`}
                  description="Tutors depending on this model may stop working."
                  okText="Delete"
                  okButtonProps={{ danger: true }}
                  onConfirm={() => remove(model)}
                >
                  <Button danger aria-label={`Delete ${config.singular}`} title={`Delete ${config.singular}`} icon={<DeleteOutlined />} loading={deletingId === model.id} />
                </Popconfirm>
              ) : null}
            </Space>
          ),
        }]
      : []),
  ];
}

export default function ModelCatalogManager({ kind }) {
  const [form] = Form.useForm();
  const { canWrite, canDelete } = useRolePermissions();
  const {
    config,
    isEmbedding,
    engines,
    loading,
    refreshing,
    error,
    engineError,
    open,
    editingModel,
    saving,
    formError,
    search,
    setSearch,
    deletingId,
    discoveredModelsByEngine,
    fetchingEngineModels,
    engineProbeError,
    filteredModels,
    load,
    loadEngineModels,
    openCreate,
    openEdit,
    close,
    save,
    remove,
  } = useModelCatalog(kind, form);

  const selectedEngineId = Form.useWatch("runtimeEngineId", form);

  useEffect(() => {
    if (open && selectedEngineId) {
      const timer = window.setTimeout(() => {
        loadEngineModels(selectedEngineId);
      }, 0);
      return () => window.clearTimeout(timer);
    }
  }, [open, selectedEngineId, loadEngineModels]);

  const columns = buildColumns({ config, isEmbedding, canWrite, canDelete, deletingId, openEdit, remove });

  return (
    <main className="workspace-page">
      <div className="workspace-page-inner">
        <PageHeader
          title={config.title}
          description={config.description}
          extra={[
            <Button key="refresh" icon={<ReloadOutlined />} onClick={() => load(true)} loading={refreshing}>Refresh</Button>,
            canWrite ? <Button key="add" type="primary" icon={<PlusOutlined />} onClick={openCreate}>Add model</Button> : null,
          ].filter(Boolean)}
        />
        {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}
        <Input className="management-search" prefix={<SearchOutlined />} allowClear value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search models" />
        <Table
          className="workspace-table"
          rowKey="id"
          columns={columns}
          dataSource={filteredModels}
          loading={loading}
          size="middle"
          pagination={{ pageSize: 10, showSizeChanger: true }}
          locale={{ emptyText: <Empty description={`No ${config.title.toLowerCase()} found`} /> }}
        />
      </div>

      <Drawer
        title={editingModel ? `Edit ${config.singular}` : `Add ${config.singular}`}
        open={open}
        onClose={close}
        size={560}
        destroyOnHidden
        footer={
          <Space style={{ width: "100%", justifyContent: "flex-end" }}>
            <Button onClick={close} disabled={saving}>Cancel</Button>
            <Button type="primary" onClick={() => form.submit()} loading={saving} disabled={!editingModel && !engines.length}>Save model</Button>
          </Space>
        }
      >
        {formError ? <Alert type="error" title={formError} showIcon style={{ marginBottom: 16 }} /> : null}
        {engineError ? <Alert type="error" title={engineError} showIcon style={{ marginBottom: 16 }} /> : null}
        {!editingModel && !engines.length && !engineError ? (
          <Alert type="warning" title="No enabled runtime engines are available. Add or enable a runtime engine first." showIcon style={{ marginBottom: 16 }} />
        ) : null}
        <Form form={form} layout="vertical" onFinish={save} requiredMark={false} initialValues={{ isEnabled: true, embeddingDimensions: 0, supportsImageInput: false, supportsFunctionCalling: false, supportsStructuredOutput: false }}>
          <Form.Item label="Name" name="name" rules={[{ required: true, whitespace: true, message: "Enter a model name." }]}>
            <Input placeholder={`My ${isEmbedding ? "Embedding" : "Interaction"} Model`} />
          </Form.Item>
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
                      const matchedDim = isEmbedding ? matchEmbeddingDimension(modelName) : undefined;

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
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {selectedEngineId ? "No models reported by provider or endpoint is currently offline." : "Select a runtime engine above to load available provider models."}
                  </Typography.Text>
                ) : null}
              </div>
            )}
          >
            <Input placeholder={config.providerPlaceholder} />
          </Form.Item>
          {isEmbedding ? (
            <Form.Item label="Embedding dimensions" name="embeddingDimensions" extra={editingModel ? "Dimensions cannot be changed after a model is created." : undefined}>
              <InputNumber min={0} precision={0} style={{ width: "100%" }} disabled={Boolean(editingModel)} />
            </Form.Item>
          ) : null}
          {!isEmbedding ? (
            <section aria-label="Capabilities">
              <Typography.Title level={5}>Capabilities</Typography.Title>
              <Form.Item label="Image input / Vision" name="supportsImageInput" valuePropName="checked" extra="Allows tutors using this model to receive images. Activate a new tutor revision to apply capability changes."><Switch /></Form.Item>
              <Form.Item label="Function calling" name="supportsFunctionCalling" valuePropName="checked" extra="Allows snapshot-configured tutor tools. Enable this only for provider models verified to support tool calls."><Switch /></Form.Item>
              <Form.Item label="Structured output" name="supportsStructuredOutput" valuePropName="checked" extra="Records that the provider model supports schema-constrained structured responses."><Switch /></Form.Item>
              {editingModel ? <ModelImageInputTest key={editingModel.id} modelId={editingModel.id} disabled={saving} /> : null}
            </section>
          ) : null}
          <Form.Item label="Settings JSON" name="settingsJson">
            <Input.TextArea className="workspace-code" autoSize={{ minRows: 4, maxRows: 12 }} placeholder='{"temperature": 0.7}' />
          </Form.Item>
          <Form.Item label="Enabled" name="isEnabled" valuePropName="checked"><Switch /></Form.Item>
        </Form>
      </Drawer>
    </main>
  );
}
