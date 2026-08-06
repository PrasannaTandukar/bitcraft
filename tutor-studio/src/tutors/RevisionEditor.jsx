import { useEffect, useState } from "react";
import Alert from "react-bootstrap/Alert";
import Button from "react-bootstrap/Button";
import Col from "react-bootstrap/Col";
import Collapse from "react-bootstrap/Collapse";
import Form from "react-bootstrap/Form";
import Modal from "react-bootstrap/Modal";
import Row from "react-bootstrap/Row";
import Spinner from "react-bootstrap/Spinner";
import { getApiErrorDescription } from "../api/client.js";
import { updateTutorRevision } from "./tutorApi.js";
import { deploymentTarget, parseJsonObject, prettyJson, revisionStatus } from "./tutorUtils.js";

const commonParameterValue = (parameters, snakeCase, camelCase) => {
  const value = parameters[snakeCase] ?? parameters[camelCase];
  return value === null || value === undefined ? "" : String(value);
};

export default function RevisionEditor({ tutorId, revision, prompts, interactionModels, embeddingModels, onClose, onSaved, onCreatePrompt }) {
  let initialParameters = {};
  try { initialParameters = parseJsonObject(revision.interactionParametersJson); } catch { initialParameters = {}; }
  const editable = revisionStatus(revision.status) === "Draft";
  const [promptVersionId, setPromptVersionId] = useState(revision.promptVersionId || "");
  const [interactionModelId, setInteractionModelId] = useState(revision.interactionModelId || "");
  const [queryEmbeddingModelId, setQueryEmbeddingModelId] = useState(revision.queryEmbeddingModelId || "");
  const [target, setTarget] = useState(String(revision.deploymentTarget ?? 0));
  const [temperature, setTemperature] = useState(commonParameterValue(initialParameters, "temperature", "temperature"));
  const [topP, setTopP] = useState(commonParameterValue(initialParameters, "top_p", "topP"));
  const [maximumTokens, setMaximumTokens] = useState(commonParameterValue(initialParameters, "max_tokens", "maximumTokens"));
  const [parametersJson, setParametersJson] = useState(prettyJson(revision.interactionParametersJson));
  const [deploymentJson, setDeploymentJson] = useState(prettyJson(revision.deploymentConfigJson));
  const [advancedOpen, setAdvancedOpen] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const interactionOptions = interactionModelId && !interactionModels.some((model) => model.id === interactionModelId)
    ? [...interactionModels, { id: interactionModelId, name: revision.interactionModelName || "Unavailable model", providerModelName: revision.providerModelName || "Not selectable" }]
    : interactionModels;
  const embeddingOptions = queryEmbeddingModelId && !embeddingModels.some((model) => model.id === queryEmbeddingModelId)
    ? [...embeddingModels, { id: queryEmbeddingModelId, name: revision.queryEmbeddingModelName || "Unavailable model", providerModelName: revision.queryEmbeddingProviderModelName || "Not selectable", embeddingDimensions: revision.queryEmbeddingDimensions || "Unknown" }]
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

  const change = (setter) => (event) => {
    setter(event.target.value);
    setDirty(true);
  };

  const requestClose = () => {
    if (dirty && !window.confirm("Discard your unsaved revision changes?")) return;
    onClose();
  };

  const createPromptShortcut = () => {
    if (dirty && !window.confirm("Leave this editor and discard your unsaved revision changes?")) return;
    onCreatePrompt();
  };

  const save = async (event) => {
    event.preventDefault();
    if (!editable || saving) return;

    let parameters;
    let deployment;
    try {
      parameters = parseJsonObject(parametersJson);
      deployment = parseJsonObject(deploymentJson);
    } catch (jsonError) {
      setError(jsonError.message || "Advanced configuration must be valid JSON objects.");
      return;
    }

    const numericFields = [
      ["temperature", temperature, 0, 2],
      ["top_p", topP, 0, 1],
      ["max_tokens", maximumTokens, 1, Number.MAX_SAFE_INTEGER],
    ];
    for (const [key, value, min, max] of numericFields) {
      if (value === "") {
        delete parameters[key];
        continue;
      }
      const number = Number(value);
      if (!Number.isFinite(number) || number < min || number > max) {
        setError(`${key.replaceAll("_", " ")} must be a number between ${min} and ${max}.`);
        return;
      }
      parameters[key] = key === "max_tokens" ? Math.trunc(number) : number;
    }

    setSaving(true);
    setError("");
    try {
      await updateTutorRevision(tutorId, revision.id, {
        promptVersionId: promptVersionId || null,
        interactionModelId: interactionModelId || null,
        queryEmbeddingModelId: queryEmbeddingModelId || null,
        interactionParametersJson: Object.keys(parameters).length ? JSON.stringify(parameters) : null,
        deploymentTarget: Number(target),
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
    <Modal show onHide={requestClose} size="lg" centered scrollable>
      <Form onSubmit={save}>
        <Modal.Header closeButton><Modal.Title>{editable ? "Configure" : "View"} Revision {revision.versionNumber}</Modal.Title></Modal.Header>
        <Modal.Body>
          {!editable && <Alert variant="info">{revisionStatus(revision.status)} revisions are immutable. Create a new Draft to change this configuration.</Alert>}
          {error && <Alert variant="danger">{error}</Alert>}

          <Form.Group className="mb-3" controlId="revision-prompt-version">
            <Form.Label>Prompt version *</Form.Label>
            {prompts.length === 0 ? (
              <div className="tutor-inline-empty"><span>No prompt version is available.</span>{editable && <Button size="sm" variant="outline-primary" onClick={createPromptShortcut}>Create prompt version</Button>}</div>
            ) : (
              <Form.Select value={promptVersionId} onChange={change(setPromptVersionId)} disabled={!editable}>
                <option value="">Select a prompt version</option>
                {prompts.map((prompt) => <option key={prompt.id} value={prompt.id}>Prompt v{prompt.versionNumber} · {prompt.name}</option>)}
              </Form.Select>
            )}
          </Form.Group>

          <Form.Group className="mb-3" controlId="revision-interaction-model">
            <Form.Label>Interaction model *</Form.Label>
            {interactionOptions.length === 0 ? (
              <Alert variant="warning" className="mb-0">No enabled interaction model is available. An administrator must configure one in Model Catalog.</Alert>
            ) : (
              <Form.Select value={interactionModelId} onChange={change(setInteractionModelId)} disabled={!editable}>
                <option value="">Select an interaction model</option>
                {interactionOptions.map((model) => <option key={model.id} value={model.id}>{model.name} · {model.providerModelName}</option>)}
              </Form.Select>
            )}
          </Form.Group>

          <Form.Group className="mb-4" controlId="revision-embedding-model">
            <Form.Label>Query embedding model</Form.Label>
            <Form.Select value={queryEmbeddingModelId} onChange={change(setQueryEmbeddingModelId)} disabled={!editable}>
              <option value="">None</option>
              {embeddingOptions.map((model) => <option key={model.id} value={model.id}>{model.name} · {model.providerModelName} · {model.embeddingDimensions} dimensions</option>)}
            </Form.Select>
            <Form.Text>Used to embed knowledge-search queries when this tutor uses retrieved documents.</Form.Text>
          </Form.Group>

          <h3 className="revision-editor-subheading">Interaction parameters</h3>
          <Row className="g-3 mb-4">
            <Col md={4}><Form.Group controlId="revision-temperature"><Form.Label>Temperature</Form.Label><Form.Control type="number" min="0" max="2" step="0.05" value={temperature} onChange={change(setTemperature)} disabled={!editable} /></Form.Group></Col>
            <Col md={4}><Form.Group controlId="revision-top-p"><Form.Label>Top P</Form.Label><Form.Control type="number" min="0" max="1" step="0.05" value={topP} onChange={change(setTopP)} disabled={!editable} /></Form.Group></Col>
            <Col md={4}><Form.Group controlId="revision-max-tokens"><Form.Label>Maximum tokens</Form.Label><Form.Control type="number" min="1" step="1" value={maximumTokens} onChange={change(setMaximumTokens)} disabled={!editable} /></Form.Group></Col>
          </Row>

          <Form.Group className="mb-3" controlId="revision-deployment-target">
            <Form.Label>Deployment</Form.Label>
            <Form.Select value={target} onChange={change(setTarget)} disabled={!editable}>
              <option value="0">Local</option>
              <option value="1">Server</option>
            </Form.Select>
            <Form.Text>Current selection: {deploymentTarget(Number(target))}. Secret values must not be entered here.</Form.Text>
          </Form.Group>

          <Button type="button" variant="link" className="px-0" onClick={() => setAdvancedOpen((value) => !value)} aria-expanded={advancedOpen} aria-controls="revision-advanced-settings">
            {advancedOpen ? "Hide advanced JSON" : "Show advanced JSON"}
          </Button>
          <Collapse in={advancedOpen}>
            <div id="revision-advanced-settings">
              <Alert variant="secondary">Advanced JSON is stored as data only. Do not include credentials, tokens, or secret references.</Alert>
              <Form.Group className="mb-3" controlId="revision-parameters-json">
                <Form.Label>Interaction parameters JSON</Form.Label>
                <Form.Control as="textarea" rows={7} className="font-monospace" value={parametersJson} onChange={change(setParametersJson)} disabled={!editable} />
              </Form.Group>
              <Form.Group controlId="revision-deployment-json">
                <Form.Label>Deployment configuration JSON</Form.Label>
                <Form.Control as="textarea" rows={5} className="font-monospace" value={deploymentJson} onChange={change(setDeploymentJson)} disabled={!editable} />
              </Form.Group>
            </div>
          </Collapse>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="outline-secondary" onClick={requestClose} disabled={saving}>Close</Button>
          {editable && <Button type="submit" disabled={saving}>{saving && <Spinner size="sm" className="me-2" />}Save draft</Button>}
        </Modal.Footer>
      </Form>
    </Modal>
  );
}
