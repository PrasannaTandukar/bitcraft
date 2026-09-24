import { Alert, App, Button, Col, Collapse, Drawer, Form, Input, InputNumber, Row, Select, Space, Typography } from "antd";
import { useEffect, useState } from "react";
import { getApiErrorDescription } from "../../../api/client.js";
import { updateTutorRevision } from "../hooks/tutorApi.js";
import { deploymentTarget, parseJsonObject, prettyJson, revisionStatus } from "../utils/tutorUtils.js";

const commonParameterValue = (parameters, snakeCase, camelCase) => {
  const value = parameters[snakeCase] ?? parameters[camelCase];
  return value === null || value === undefined ? null : Number(value);
};

export default function RevisionEditor({ tutorId, revision, prompts, interactionModels, embeddingModels, onClose, onSaved, onCreatePrompt }) {
  const { modal } = App.useApp();
  let initialParameters = {};
  try { initialParameters = parseJsonObject(revision.interactionParametersJson); } catch { initialParameters = {}; }
  const editable = revisionStatus(revision.status) === "Draft";
  const [form] = Form.useForm();
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const interactionOptions = revision.interactionModelId && !interactionModels.some((model) => model.id === revision.interactionModelId)
    ? [...interactionModels, { id: revision.interactionModelId, name: revision.interactionModelName || "Unavailable model", providerModelName: revision.providerModelName || "Not selectable" }]
    : interactionModels;
  const embeddingOptions = revision.queryEmbeddingModelId && !embeddingModels.some((model) => model.id === revision.queryEmbeddingModelId)
    ? [...embeddingModels, { id: revision.queryEmbeddingModelId, name: revision.queryEmbeddingModelName || "Unavailable model", providerModelName: revision.queryEmbeddingProviderModelName || "Not selectable", embeddingDimensions: revision.queryEmbeddingDimensions || "Unknown" }]
    : embeddingModels;

  useEffect(() => {
    if (!dirty) return undefined;
    const warn = (event) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);

  const requestClose = () => {
    if (!dirty) {
      onClose();
      return;
    }
    modal.confirm({ title: "Discard unsaved changes?", content: "Your revision changes have not been saved.", okText: "Discard", okButtonProps: { danger: true }, onOk: onClose });
  };

  const createPromptShortcut = () => {
    if (!dirty) {
      onCreatePrompt();
      return;
    }
    modal.confirm({ title: "Leave the revision editor?", content: "Your unsaved changes will be discarded.", okText: "Leave editor", okButtonProps: { danger: true }, onOk: onCreatePrompt });
  };

  const save = async (values) => {
    console.log("REVISION FORM VALUES:", values);
    if (!editable || saving) return;
    let parameters;
    let deployment;
    try {
      parameters = parseJsonObject(values.parametersJson);
      deployment = parseJsonObject(values.deploymentJson);
    } catch (jsonError) {
      setError(jsonError.message || "Advanced configuration must be valid JSON objects.");
      return;
    }

    const numericFields = [
      ["temperature", values.temperature],
      ["top_p", values.topP],
      ["max_tokens", values.maximumTokens],
    ];
    numericFields.forEach(([key, value]) => {
      if (value === null || value === undefined || value === "") delete parameters[key];
      else parameters[key] = key === "max_tokens" ? Math.trunc(value) : value;
    });

    setSaving(true);
    setError("");
    try {
      await updateTutorRevision(tutorId, revision.id, {
        promptVersionId: values.promptVersionId || null,
        interactionModelId: values.interactionModelId || null,
        queryEmbeddingModelId: values.queryEmbeddingModelId || null,
        interactionParametersJson: Object.keys(parameters).length ? JSON.stringify(parameters) : null,
        deploymentTarget: Number(values.deploymentTarget),
        deploymentConfigJson: Object.keys(deployment).length ? JSON.stringify(deployment) : null,
      });
      setDirty(false);
      onSaved();
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "Could not save the revision configuration."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Drawer
      title={`${editable ? "Configure" : "View"} revision ${revision.versionNumber}`}
      open
      onClose={requestClose}
      size={720}
      destroyOnHidden
      footer={(
        <Space style={{ width: "100%", justifyContent: "flex-end" }}>
          <Button onClick={requestClose} disabled={saving}>Close</Button>
          {editable ? <Button type="primary" onClick={() => form.submit()} loading={saving}>Save draft</Button> : null}
        </Space>
      )}
    >
      {!editable ? <Alert type="info" title={`${revisionStatus(revision.status)} revisions are immutable`} description="Create a new Draft to change this configuration." showIcon style={{ marginBottom: 16 }} /> : null}
      {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}

      <Form
        form={form}
        layout="vertical"
        disabled={!editable}
        onFinish={save}
        onValuesChange={(changedValues, allValues) => {
    console.log("FORM CHANGED:", changedValues, allValues);
    setDirty(true);
  }}
        initialValues={{
          promptVersionId: revision.promptVersionId || undefined,
          interactionModelId: revision.interactionModelId || undefined,
          queryEmbeddingModelId: revision.queryEmbeddingModelId || undefined,
          temperature: commonParameterValue(initialParameters, "temperature", "temperature"),
          topP: commonParameterValue(initialParameters, "top_p", "topP"),
          maximumTokens: commonParameterValue(initialParameters, "max_tokens", "maximumTokens"),
          deploymentTarget: String(revision.deploymentTarget ?? 0),
          parametersJson: prettyJson(revision.interactionParametersJson),
          deploymentJson: prettyJson(revision.deploymentConfigJson),
        }}
      >
        <Form.Item label="Prompt version" name="promptVersionId" extra={!prompts.length && editable ? <Button type="link" onClick={createPromptShortcut}>Create a prompt version</Button> : null}>
          <Select allowClear placeholder="Select a prompt version" options={prompts.map((prompt) => ({ value: prompt.id, label: `Prompt v${prompt.versionNumber} · ${prompt.name}` }))} />
        </Form.Item>

        <Form.Item label="Interaction model" name="interactionModelId">
          <Select allowClear placeholder="Select an interaction model" options={interactionOptions.map((model) => ({ value: model.id, label: `${model.name} · ${model.providerModelName}` }))} />
        </Form.Item>
        {!interactionOptions.length ? <Alert type="warning" title="No enabled interaction model is available. An administrator must configure one in Model Catalog." showIcon style={{ marginBottom: 16 }} /> : null}

        <Form.Item label="Query embedding model" name="queryEmbeddingModelId" extra="Used to embed knowledge-search queries when the tutor retrieves documents.">
          <Select allowClear placeholder="None" options={embeddingOptions.map((model) => ({ value: model.id, label: `${model.name} · ${model.providerModelName} · ${model.embeddingDimensions} dimensions` }))} />
        </Form.Item>

        <Typography.Title level={5}>Interaction parameters</Typography.Title>
        <Row gutter={16}>
          <Col xs={24} md={8}><Form.Item label="Temperature" name="temperature"><InputNumber min={0} max={2} step={0.05} style={{ width: "100%" }} /></Form.Item></Col>
          <Col xs={24} md={8}><Form.Item label="Top P" name="topP"><InputNumber min={0} max={1} step={0.05} style={{ width: "100%" }} /></Form.Item></Col>
          <Col xs={24} md={8}><Form.Item label="Maximum tokens" name="maximumTokens"><InputNumber min={1} precision={0} style={{ width: "100%" }} /></Form.Item></Col>
        </Row>

        <Form.Item label="Deployment" name="deploymentTarget" extra={`The stored value is ${deploymentTarget(revision.deploymentTarget)}. Do not enter secrets in deployment JSON.`}>
          <Select options={[{ value: "0", label: "Local" }, { value: "1", label: "Server" }]} />
        </Form.Item>

        <Collapse
          ghost
          items={[{
            key: "advanced",
            label: "Advanced JSON configuration",
            children: (
              <>
                <Alert type="info" title="Advanced JSON is stored as data only. Do not include credentials, tokens, or secret references." showIcon style={{ marginBottom: 16 }} />
                <Form.Item label="Interaction parameters JSON" name="parametersJson"><Input.TextArea className="workspace-code" autoSize={{ minRows: 7, maxRows: 14 }} /></Form.Item>
                <Form.Item label="Deployment configuration JSON" name="deploymentJson"><Input.TextArea className="workspace-code" autoSize={{ minRows: 5, maxRows: 12 }} /></Form.Item>
              </>
            ),
          }]}
        />
      </Form>
    </Drawer>
  );
}
