import { PlusOutlined } from "@ant-design/icons";
import { Alert, Button, Checkbox, Collapse, Drawer, Empty, Flex, Form, Input, Space, Tag, Typography } from "antd";
import { useState } from "react";
import { getApiErrorDescription } from "../api/client.js";
import { createPromptVersion } from "./tutorApi.js";
import { formatDateTime } from "./tutorUtils.js";

export default function PromptVersionsPanel({ tutorId, prompts, activePromptVersionId, canManage, onChanged }) {
  const [form] = Form.useForm();
  const [showForm, setShowForm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const close = () => {
    if (saving) return;
    form.resetFields();
    setError("");
    setShowForm(false);
  };

  const submit = async (values) => {
    setSaving(true);
    setError("");
    try {
      await createPromptVersion(tutorId, {
        name: values.name.trim(),
        content: values.content,
        notes: values.notes?.trim() || null,
        setAsActive: Boolean(values.setAsActive),
      });
      form.resetFields();
      setError("");
      setShowForm(false);
      onChanged();
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "Could not create the prompt version."));
    } finally {
      setSaving(false);
    }
  };

  const items = prompts.map((prompt) => ({
    key: prompt.id,
    label: (
      <Flex gap={8} align="center" wrap>
        <Typography.Text strong>Prompt v{prompt.versionNumber} · {prompt.name}</Typography.Text>
        {activePromptVersionId === prompt.id ? <Tag>Legacy active</Tag> : null}
      </Flex>
    ),
    children: (
      <Space orientation="vertical" size={16} style={{ width: "100%" }}>
        <Flex gap={24} wrap>
          <Typography.Text type="secondary">Created {formatDateTime(prompt.createdUtc)}</Typography.Text>
          <Typography.Text type="secondary">Notes: {prompt.notes || "—"}</Typography.Text>
        </Flex>
        <Typography.Text strong>Prompt content</Typography.Text>
        <pre className="workspace-code prompt-content-view">{prompt.content}</pre>
      </Space>
    ),
  }));

  return (
    <div className="tutor-panel-stack">
      <Flex justify="space-between" align="flex-start" gap={16} wrap>
        <div>
          <Typography.Title level={4}>Prompt versions</Typography.Title>
          <Typography.Paragraph type="secondary">Immutable tutor instruction history. Create a new version whenever instructions change.</Typography.Paragraph>
        </div>
        {canManage ? <Button type="primary" icon={<PlusOutlined />} onClick={() => setShowForm(true)}>Create prompt version</Button> : null}
      </Flex>

      <Alert type="info" title="Prompt versions are immutable" description="Create a new version to make changes. The legacy active prompt is separate from the active Tutor Revision." showIcon />

      {prompts.length ? <Collapse items={items} /> : (
        <Empty description="No prompt versions">
          {canManage ? <Button type="primary" onClick={() => setShowForm(true)}>Create first prompt</Button> : null}
        </Empty>
      )}

      <Drawer
        title="Create prompt version"
        open={showForm && canManage}
        onClose={close}
        size={640}
        destroyOnHidden
        footer={(
          <Space style={{ width: "100%", justifyContent: "flex-end" }}>
            <Button onClick={close} disabled={saving}>Cancel</Button>
            <Button type="primary" onClick={() => form.submit()} loading={saving}>Create immutable version</Button>
          </Space>
        )}
      >
        {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}
        <Form form={form} layout="vertical" onFinish={submit} requiredMark={false} initialValues={{ setAsActive: false }}>
          <Form.Item label="Name" name="name" rules={[{ required: true, whitespace: true, message: "Enter a version name." }]}>
            <Input placeholder="Robotics Tutor Instructions" maxLength={200} showCount />
          </Form.Item>
          <Form.Item label="Prompt content" name="content" rules={[{ required: true, whitespace: true, message: "Enter the prompt instructions." }]}>
            <Input.TextArea rows={12} placeholder="You are a helpful robotics tutor…" />
          </Form.Item>
          <Form.Item label="Notes" name="notes">
            <Input.TextArea rows={3} placeholder="What changed in this version?" />
          </Form.Item>
          <Form.Item name="setAsActive" valuePropName="checked">
            <Checkbox>Set as legacy active prompt</Checkbox>
          </Form.Item>
        </Form>
      </Drawer>
    </div>
  );
}
