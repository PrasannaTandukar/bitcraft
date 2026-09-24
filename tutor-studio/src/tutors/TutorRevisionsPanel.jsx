import { PlusOutlined } from "@ant-design/icons";
import { Alert, App, Button, Collapse, Descriptions, Empty, Flex, Space, Spin, Tag, Typography } from "antd";
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { getApiErrorDescription } from "../api/client.js";
import StatusTag from "../components/StatusTag.jsx";
import RevisionEditor from "./RevisionEditor.jsx";
import {
  activateTutorRevision,
  archiveTutorRevision,
  createTutorRevision,
  discardTutorRevision,
  publishTutorRevision,
} from "./tutorApi.js";
import { deploymentTarget, formatDateTime, revisionIsPublishable, revisionStatus } from "./tutorUtils.js";

const confirmationText = (action) => ({
  publish: "Published revisions become immutable and can then be activated.",
  activate: "This revision will become the tutor's runtime configuration. The previously active revision will be archived.",
  archive: "The tutor will stop using this revision until another revision is activated.",
  discard: "This Draft revision will be permanently discarded.",
}[action]);

export default function TutorRevisionsPanel({ tutorId, revisions, prompts, interactionModels, embeddingModels, canManage, onChanged, onCreatePrompt }) {
  const navigate = useNavigate();
  const { modal } = App.useApp();
  const [editing, setEditing] = useState(null);
  const [busy, setBusy] = useState("");
  const [error, setError] = useState("");

  const createRevision = async () => {
    if (busy) return;
    setBusy("create");
    setError("");
    try {
      const created = await createTutorRevision(tutorId);
      setEditing(created);
      onChanged();
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "Could not create a Draft revision."));
    } finally {
      setBusy("");
    }
  };

  const perform = (revision, action, request) => {
    if (busy) return;
    modal.confirm({
      title: `${action[0].toUpperCase()}${action.slice(1)} revision ${revision.versionNumber}?`,
      content: confirmationText(action),
      okText: action === "discard" ? "Discard" : `${action[0].toUpperCase()}${action.slice(1)}`,
      okButtonProps: { danger: action === "discard" },
      async onOk() {
        setBusy(`${revision.id}:${action}`);
        setError("");
        try {
          await request(tutorId, revision.id);
          if (editing?.id === revision.id) setEditing(null);
          onChanged();
        } catch (requestError) {
          setError(getApiErrorDescription(requestError, `Could not ${action} the revision.`));
          throw requestError;
        } finally {
          setBusy("");
        }
      },
    });
  };

  const items = revisions.map((revision) => {
    const status = revisionStatus(revision.status);
    const isBusy = busy.startsWith(String(revision.id));
    const missing = [
      !revision.promptVersionId && "prompt version",
      !revision.interactionModelId && "interaction model",
      revision.interactionModelEnabled === false && "enabled interaction model",
      revision.runtimeEngineEnabled === false && "enabled runtime engine",
    ].filter(Boolean);

    const actions = [];
    if (status === "Draft" && canManage) {
      actions.push(<Button key="configure" size="small" onClick={() => setEditing(revision)}>Configure</Button>);
      actions.push(<Button key="publish" size="small" type="primary" disabled={!revisionIsPublishable(revision) || isBusy} onClick={() => perform(revision, "publish", publishTutorRevision)}>Publish</Button>);
      actions.push(<Button key="discard" size="small" danger disabled={isBusy} onClick={() => perform(revision, "discard", discardTutorRevision)}>Discard</Button>);
    }
    if (status === "Published" && canManage) {
      actions.push(<Button key="view" size="small" onClick={() => setEditing(revision)}>View configuration</Button>);
      actions.push(<Button key="activate" size="small" type="primary" disabled={isBusy} onClick={() => perform(revision, "activate", activateTutorRevision)}>Activate</Button>);
      actions.push(<Button key="archive" size="small" disabled={isBusy} onClick={() => perform(revision, "archive", archiveTutorRevision)}>Archive</Button>);
    }
    if (status === "Active" && canManage) {
      actions.push(<Button key="view" size="small" onClick={() => setEditing(revision)}>View</Button>);
      actions.push(<Button key="archive" size="small" disabled={isBusy} onClick={() => perform(revision, "archive", archiveTutorRevision)}>Archive</Button>);
      actions.push(<Button key="benchmark" size="small" type="primary" onClick={() => navigate(`/benchmarking?tab=new&tutorId=${tutorId}&revisionId=${revision.id}`)}>Benchmark</Button>);
    }
    if (status === "Archived" || !canManage) actions.push(<Button key="view" size="small" onClick={() => setEditing(revision)}>View configuration</Button>);

    return {
      key: revision.id,
      label: (
        <Flex gap={8} align="center" wrap>
          <Typography.Text strong>Revision {revision.versionNumber}</Typography.Text>
          <StatusTag value={status} />
          {revision.isActive ? <Tag color="blue">Current runtime</Tag> : null}
        </Flex>
      ),
      children: (
        <Space orientation="vertical" size={16} style={{ width: "100%" }}>
          <Descriptions size="small" column={{ xs: 1, sm: 2, xl: 3 }}>
            <Descriptions.Item label="Prompt version">{revision.promptVersionNumber ? `Prompt v${revision.promptVersionNumber} · ${revision.promptVersionName}` : "Not selected"}</Descriptions.Item>
            <Descriptions.Item label="Interaction model">{revision.interactionModelName ? `${revision.interactionModelName} · ${revision.providerModelName}` : "Not selected"}</Descriptions.Item>
            <Descriptions.Item label="Query embedding">{revision.queryEmbeddingModelName ? `${revision.queryEmbeddingModelName} · ${revision.queryEmbeddingDimensions} dimensions` : "None"}</Descriptions.Item>
            <Descriptions.Item label="Deployment">{revision.deploymentTargetDisplay || deploymentTarget(revision.deploymentTarget)}</Descriptions.Item>
            <Descriptions.Item label="Created">{formatDateTime(revision.createdUtc)}</Descriptions.Item>
            <Descriptions.Item label="Published">{formatDateTime(revision.publishedUtc)}</Descriptions.Item>
            <Descriptions.Item label="Activated">{formatDateTime(revision.activatedUtc)}</Descriptions.Item>
            <Descriptions.Item label="Archived">{formatDateTime(revision.archivedUtc)}</Descriptions.Item>
            <Descriptions.Item label="Activation slug"><Typography.Text code copyable>{revision.activationSlug || "—"}</Typography.Text></Descriptions.Item>
            <Descriptions.Item label="Snapshot"><StatusTag value={revision.latestSnapshotId ? "Ready" : "Not captured"} /></Descriptions.Item>
          </Descriptions>

          {status === "Draft" && missing.length ? <Alert type="warning" title={`Before publishing, select: ${missing.join(" and ")}.`} showIcon /> : null}

          <Flex gap={8} align="center" wrap>
            {actions}
            {isBusy ? <Typography.Text type="secondary"><Spin size="small" /> Updating revision…</Typography.Text> : null}
          </Flex>
        </Space>
      ),
    };
  });

  return (
    <div className="tutor-panel-stack">
      <Flex justify="space-between" align="flex-start" gap={16} wrap>
        <div>
          <Typography.Title level={4}>Revisions</Typography.Title>
          <Typography.Paragraph type="secondary">Versioned, reproducible runtime configurations for this tutor.</Typography.Paragraph>
        </div>
        {canManage ? <Button type="primary" icon={<PlusOutlined />} onClick={createRevision} loading={busy === "create"}>Create revision</Button> : null}
      </Flex>

      {error ? <Alert type="error" title={error} showIcon /> : null}
      {revisions.length ? (
        <Collapse items={items} defaultActiveKey={revisions.length === 1 ? [revisions[0].id] : undefined} />
      ) : (
        <Empty description="No revisions">
          {canManage ? <Button type="primary" onClick={createRevision} loading={busy === "create"}>Create first revision</Button> : null}
        </Empty>
      )}

      {editing ? (
        <RevisionEditor
          key={editing.id}
          tutorId={tutorId}
          revision={editing}
          revisions={revisions}
          prompts={prompts}
          interactionModels={interactionModels}
          embeddingModels={embeddingModels}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); onChanged(); }}
          onCreatePrompt={() => { setEditing(null); onCreatePrompt(); }}
        />
      ) : null}
    </div>
  );
}
