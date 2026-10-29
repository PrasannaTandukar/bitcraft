import { AppstoreAddOutlined, EditOutlined, PlusOutlined } from "@ant-design/icons";
import { Alert, Button, Checkbox, Collapse, Drawer, Empty, Flex, Form, Input, Segmented, Space, Tag, Typography } from "antd";
import { useState } from "react";
import { getApiErrorDescription } from "../../../api/client.js";
import { createPromptVersion } from "../hooks/tutorApi.js";
import { formatDateTime } from "../utils/tutorUtils.js";

function buildStructuredPrompt({ steering = "", toneStyle = "", guardrails = "", responseFormat = "" }) {
  const sections = [];
  if (steering.trim()) {
    sections.push(`## Role & Steering\n${steering.trim()}`);
  }
  if (toneStyle.trim()) {
    sections.push(`## Tone & Style Guidelines\n${toneStyle.trim()}`);
  }
  if (guardrails.trim()) {
    sections.push(`## Pedagogical Guardrails & Boundaries\n${guardrails.trim()}`);
  }
  if (responseFormat.trim()) {
    sections.push(`## Response Format & Output Structure\n${responseFormat.trim()}`);
  }
  return sections.join("\n\n");
}

export default function PromptVersionsPanel({ tutorId, prompts, activePromptVersionId, canManage, onChanged }) {
  const [form] = Form.useForm();
  const [showForm, setShowForm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [builderMode, setBuilderMode] = useState("structured");

  const handleValuesChange = (_, allValues) => {
    if (builderMode === "structured") {
      const generated = buildStructuredPrompt({
        steering: allValues.steering || "",
        toneStyle: allValues.toneStyle || "",
        guardrails: allValues.guardrails || "",
        responseFormat: allValues.responseFormat || "",
      });
      form.setFieldValue("content", generated);
    }
  };

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
      const finalContent = builderMode === "structured"
        ? (values.content || buildStructuredPrompt(values))
        : values.content;

      if (!finalContent || !finalContent.trim()) {
        setError("Prompt content cannot be empty.");
        setSaving(false);
        return;
      }

      await createPromptVersion(tutorId, {
        name: values.name.trim(),
        content: finalContent.trim(),
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
        size={720}
        destroyOnHidden
        footer={(
          <Space style={{ width: "100%", justifyContent: "flex-end" }}>
            <Button onClick={close} disabled={saving}>Cancel</Button>
            <Button type="primary" onClick={() => form.submit()} loading={saving}>Create immutable version</Button>
          </Space>
        )}
      >
        {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}

        <Flex justify="space-between" align="center" style={{ marginBottom: 16 }}>
          <Segmented
            value={builderMode}
            onChange={(val) => {
              setBuilderMode(val);
              if (val === "structured" && !form.getFieldValue("steering")) {
                const currentRaw = form.getFieldValue("content");
                if (currentRaw) form.setFieldValue("steering", currentRaw);
              }
            }}
            options={[
              { label: "Structured Builder", value: "structured", icon: <AppstoreAddOutlined /> },
              { label: "Raw Prompt Editor", value: "raw", icon: <EditOutlined /> },
            ]}
          />
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {builderMode === "structured" ? "Steering, tone, guardrails & output format" : "Direct markdown/template editing"}
          </Typography.Text>
        </Flex>

        <Form
          form={form}
          layout="vertical"
          onFinish={submit}
          onValuesChange={handleValuesChange}
          requiredMark={false}
          initialValues={{ setAsActive: false }}
        >
          <Form.Item label="Version name" name="name" rules={[{ required: true, whitespace: true, message: "Enter a version name." }]}>
            <Input placeholder="e.g. Socratic Robotics Guide v2" maxLength={200} showCount />
          </Form.Item>

          {builderMode === "structured" ? (
            <>
              <Form.Item
                label="System Steering & Persona"
                name="steering"
                rules={[{ required: true, whitespace: true, message: "Enter persona and pedagogical role." }]}
                extra="Who the tutor is, pedagogical mission, target audience, and subject matter context."
              >
                <Input.TextArea rows={4} placeholder="You are an expert AI tutor specializing in Robotics and Control Systems. Guide the student step-by-step with clear, foundational concepts." />
              </Form.Item>

              <Form.Item
                label="Tone & Teaching Style"
                name="toneStyle"
                extra="Socratic questioning, technical depth, direct vs exploratory, encouragement level."
              >
                <Input.TextArea rows={3} placeholder="Adopt a supportive, Socratic teaching style. Ask guiding questions before providing complete equations. Keep responses concise and focused." />
              </Form.Item>

              <Form.Item
                label="Pedagogical Guardrails & Boundaries"
                name="guardrails"
                extra="Safety boundaries, refusal rules, preventing answer-spoonfeeding, adherence to course material."
              >
                <Input.TextArea rows={3} placeholder="Never solve graded homework problems directly. Always ask the student to propose the first step. If asked about unrelated topics, politely guide back to robotics." />
              </Form.Item>

              <Form.Item
                label="Response Structure & Output Format"
                name="responseFormat"
                extra="Markdown guidelines, code block formatting, equation formats (LaTeX/KaTeX)."
              >
                <Input.TextArea rows={3} placeholder="Format equations using LaTeX/KaTeX notation ($ for inline, $$ for block). Use bold headers for multi-step explanations. End every explanation with a check-for-understanding question." />
              </Form.Item>

              <Form.Item label="Assembled Prompt Preview" name="content">
                <Input.TextArea rows={6} className="workspace-code" readOnly style={{ backgroundColor: "rgba(0,0,0,0.02)" }} />
              </Form.Item>
            </>
          ) : (
            <Form.Item
              label="Prompt content"
              name="content"
              rules={[{ required: true, whitespace: true, message: "Enter the prompt instructions." }]}
              extra="Raw instruction template with markdown headers or custom formatting."
            >
              <Input.TextArea rows={14} className="workspace-code" placeholder="You are an expert AI tutor..." />
            </Form.Item>
          )}

          <Form.Item label="Notes / Change summary" name="notes">
            <Input.TextArea rows={2} placeholder="What changed in this prompt revision?" />
          </Form.Item>

          <Form.Item name="setAsActive" valuePropName="checked">
            <Checkbox>Set as legacy active prompt</Checkbox>
          </Form.Item>
        </Form>
      </Drawer>
    </div>
  );
}
