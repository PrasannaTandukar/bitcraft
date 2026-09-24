import { PlusOutlined } from "@ant-design/icons";
import { Alert, Button, Collapse, Empty, Flex, Form, Input, Modal, Popconfirm, Select, Space, Switch, Table, Tag, Tooltip, Typography } from "antd";
import { useEffect, useState } from "react";
import { getApiErrorDescription } from "../api/client.js";
import { createTool, deleteTool, getToolCatalog, listTools, updateTool } from "./tutorToolsApi.js";

const policyNumber = (policy) => typeof policy === "number" ? policy : ({ Disabled: 0, ReadOnlyAutomatic: 1, RequiresApproval: 2 }[policy] ?? 0);
const policyLabel = (policy) => ["Disabled", "Automatic", "Requires Approval"][policyNumber(policy)];

export default function TutorToolsPanel({ tutorId, selectedIds, onSelectionChange, editable, model, revisions }) {
  const [tools, setTools] = useState([]);
  const [catalog, setCatalog] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [editing, setEditing] = useState(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [form] = Form.useForm();
  const enabled = Form.useWatch("enabled", form);
  const policy = Form.useWatch("policy", form);
  const implementationKey = Form.useWatch("implementationKey", form);
  const implementation = catalog.find((entry) => entry.implementationKey === implementationKey);

  useEffect(() => {
    const controller = new AbortController();
    Promise.all([listTools(tutorId, controller.signal), getToolCatalog(tutorId, controller.signal)])
      .then(([items, entries]) => { setTools(items); setCatalog(entries); })
      .catch((failure) => { if (!controller.signal.aborted) setError(getApiErrorDescription(failure, "Could not load tools.")); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [tutorId]);

  const openEditor = (tool) => {
    const entry = catalog.find((item) => item.implementationKey === tool?.implementationKey) || catalog[0];
    form.setFieldsValue({ implementationKey: entry?.implementationKey, name: tool?.name || entry?.name,
      description: tool?.description ?? entry?.description, enabled: tool ? policyNumber(tool.executionPolicy) !== 0 : true,
      policy: tool && policyNumber(tool.executionPolicy) !== 0 ? policyNumber(tool.executionPolicy) : 1 });
    setFormError("");
    setEditing(tool || {});
  };
  const save = async (values) => {
    setSaving(true);
    setFormError("");
    try {
      const entry = catalog.find((item) => item.implementationKey === values.implementationKey);
      const payload = { name: values.name.trim(), description: values.description?.trim(), kind: entry.kind,
        implementationKey: entry.implementationKey, executionPolicy: values.enabled ? values.policy : 0 };
      const saved = editing.id ? await updateTool(tutorId, editing.id, payload) : await createTool(tutorId, payload);
      setTools((items) => editing.id ? items.map((item) => item.id === saved.id ? saved : item) : [...items, saved]);
      if (!editing.id) onSelectionChange([...selectedIds, saved.id]);
      setEditing(null);
    } catch (failure) { setFormError(getApiErrorDescription(failure, "Could not save the tool.")); }
    finally { setSaving(false); }
  };
  const remove = async (tool) => {
    setError("");
    try {
      await deleteTool(tutorId, tool.id);
      setTools((items) => items.filter((item) => item.id !== tool.id));
      onSelectionChange(selectedIds.filter((id) => id !== tool.id));
    } catch (failure) { setError(getApiErrorDescription(failure, "Could not delete the tool.")); }
  };
  const selectedTools = tools.filter((tool) => selectedIds.includes(tool.id));
  const needsFunctionCalling = selectedTools.some((tool) => policyNumber(tool.executionPolicy) !== 0);
  const immutable = (tool) => revisions.some((revision) => ![0, "Draft"].includes(revision.status) && revision.toolDefinitionIds?.includes(tool.id));
  const referenced = (tool) => revisions.some((revision) => revision.toolDefinitionIds?.includes(tool.id));

  return <section style={{ marginTop: 24 }}>
    <Flex justify="space-between" align="center" gap={12}>
      <Typography.Title level={5}>Tools</Typography.Title>
      {editable && <Button icon={<PlusOutlined />} onClick={() => openEditor()} disabled={loading || !catalog.length}>Add Tool</Button>}
    </Flex>
    <Typography.Paragraph type="secondary">Available tutor tools are shared across drafts. Select the tools enabled for this revision, then Save draft. Activation captures their configuration for future conversations.</Typography.Paragraph>
    {error && <Alert type="error" title={error} showIcon style={{ marginBottom: 12 }} />}
    {needsFunctionCalling && !model?.supportsFunctionCalling && <Alert type="warning" showIcon style={{ marginBottom: 12 }} title="This model is not configured for function calling." description="Enable Function Calling in Model Catalog or choose another model before publishing or activating this revision." />}
    {selectedTools.some((tool) => policyNumber(tool.executionPolicy) === 2) && <Alert type="warning" showIcon style={{ marginBottom: 12 }} title="Tool approval is not available in chat yet." description="Choose Automatic for Calculator or disable it before publishing." />}
    <Table size="small" rowKey="id" loading={loading} dataSource={tools} pagination={false} scroll={{ x: 620 }}
      locale={{ emptyText: <Empty description={<><Typography.Text strong>No tools configured</Typography.Text><p>Add tools to allow this tutor to perform actions such as calculations during a conversation.</p></>}>{editable && <Button type="primary" disabled={!catalog.length} onClick={() => openEditor()}>Add Tool</Button>}</Empty> }}
      columns={[
        { title: "This revision", width: 110, render: (_, tool) => <Switch aria-label={`Enable ${tool.name} for this revision`} checked={selectedIds.includes(tool.id)} disabled={!editable} onChange={(checked) => onSelectionChange(checked ? [...selectedIds, tool.id] : selectedIds.filter((id) => id !== tool.id))} /> },
        { title: "Name", dataIndex: "name", render: (name, tool) => <Space orientation="vertical" size={0}><Typography.Text strong>{name}</Typography.Text><Typography.Text type="secondary">{catalog.find((entry) => entry.implementationKey === tool.implementationKey)?.name || "Unavailable implementation"}</Typography.Text></Space> },
        { title: "Type", render: () => <Tag>Built-in</Tag> },
        { title: "Execution policy", dataIndex: "executionPolicy", render: policyLabel },
        { title: "Status", dataIndex: "executionPolicy", render: (value) => <Tag color={policyNumber(value) ? "success" : "default"}>{policyNumber(value) ? "Enabled" : "Disabled"}</Tag> },
        ...(editable ? [{ title: "Actions", render: (_, tool) => <Space>
          <Tooltip title={immutable(tool) ? "Used by an immutable revision. Add a new tool to change its configuration." : ""}><Button size="small" disabled={immutable(tool)} onClick={() => openEditor(tool)}>Edit</Button></Tooltip>
          <Tooltip title={referenced(tool) ? "Remove from drafts and save first. Tools in immutable revisions cannot be deleted." : ""}><Popconfirm title={`Delete ${tool.name}?`} description="This removes the available tutor tool." onConfirm={() => remove(tool)} disabled={referenced(tool)}><Button size="small" danger disabled={referenced(tool)}>Delete</Button></Popconfirm></Tooltip>
        </Space> }] : []),
      ]} />
    <Modal title={editing?.id ? "Edit tool" : "Add Tool"} open={editing !== null} onCancel={() => !saving && setEditing(null)} onOk={() => form.submit()} confirmLoading={saving} okText="Save tool" destroyOnHidden>
      {formError && <Alert type="error" title={formError} showIcon style={{ marginBottom: 12 }} />}
      <Form form={form} layout="vertical" onFinish={save}>
        <Form.Item label="Tool type"><Input value="Built-in" readOnly /></Form.Item>
        <Form.Item label="Implementation" name="implementationKey" rules={[{ required: true }]}><Select options={catalog.map((entry) => ({ value: entry.implementationKey, label: entry.name }))} onChange={(key) => { const entry = catalog.find((item) => item.implementationKey === key); form.setFieldsValue({ name: entry.name, description: entry.description }); }} /></Form.Item>
        <Form.Item label="Display name" name="name" extra="Use letters, numbers, underscores or hyphens; start with a letter or underscore." rules={[{ required: true }, { pattern: /^[A-Za-z_][A-Za-z0-9_-]{0,63}$/, message: "Enter a valid tool name (up to 64 characters)." }]}><Input maxLength={64} /></Form.Item>
        <Form.Item label="Description" name="description"><Input.TextArea rows={2} maxLength={2000} /></Form.Item>
        <Form.Item label="Execution policy" name="policy"><Select options={[{ value: 1, label: "Automatic", disabled: !implementation?.supportsAutomaticExecution }, { value: 2, label: "Requires Approval — chat support coming later" }]} /></Form.Item>
        {enabled && policy === 2 && <Alert type="warning" showIcon title="Requires Approval tools will not execute automatically. Publishing is blocked until approval support is available." style={{ marginBottom: 12 }} />}
        <Form.Item label="Enabled" name="enabled" valuePropName="checked"><Switch /></Form.Item>
        <Collapse ghost items={[{ key: "schema", label: "Advanced: input schema", children: <pre style={{ whiteSpace: "pre-wrap" }}>{implementation ? JSON.stringify(JSON.parse(implementation.jsonSchema), null, 2) : ""}</pre> }]} />
      </Form>
    </Modal>
  </section>;
}
