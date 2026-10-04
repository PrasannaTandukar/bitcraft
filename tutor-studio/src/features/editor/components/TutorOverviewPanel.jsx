import { CheckCircleFilled, ClockCircleOutlined, ExperimentOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Col, Descriptions, Flex, Row, Tag, Typography } from "antd";
import { useNavigate } from "react-router-dom";
import StatusTag from "../../../components/StatusTag.jsx";
import { formatDateTime, revisionStatus, tutorStatus } from "../utils/tutorUtils.js";

export default function TutorOverviewPanel({ tutor, prompts, revisions, runtimeReadiness, canManage, onOpenTab }) {
  const navigate = useNavigate();
  const activeRevision = revisions.find((revision) => revision.id === tutor.activeRevisionId)
    || revisions.find((revision) => revisionStatus(revision.status) === "Active");
  const configuredDraft = revisions.find((revision) => revisionStatus(revision.status) === "Draft" && revision.promptVersionId && revision.interactionModelId);
  const published = revisions.find((revision) => revisionStatus(revision.status) === "Published");
  const ready = runtimeReadiness?.isChatReady === true;
  const readinessReason = runtimeReadiness?.reasons?.[0];
  const checklist = [
    { label: "Tutor created", complete: true },
    { label: "Prompt version created", complete: prompts.length > 0, action: "Create prompt", tab: "prompts" },
    { label: "Interaction model selected", complete: revisions.some((revision) => revision.interactionModelId), action: "Configure revision", tab: "revisions" },
    { label: "Draft revision configured", complete: Boolean(configuredDraft) || revisions.some((revision) => ["Published", "Active", "Archived"].includes(revisionStatus(revision.status))), action: revisions.length ? "Configure revision" : "Create revision", tab: "revisions" },
    { label: "Revision published", complete: Boolean(published || activeRevision || revisions.some((revision) => revisionStatus(revision.status) === "Archived")), action: "Publish revision", tab: "revisions" },
    { label: "Revision activated", complete: Boolean(activeRevision), action: "Activate revision", tab: "revisions" },
    { label: "Ready for chat", complete: ready, action: readinessReason?.code === "KnowledgeNotVectorized" || readinessReason?.code?.startsWith("KnowledgeEmbedding") ? "Open knowledge" : "Open revisions", tab: readinessReason?.code === "KnowledgeNotVectorized" || readinessReason?.code?.startsWith("KnowledgeEmbedding") ? "knowledge" : "revisions" },
  ];

  return (
    <div className="tutor-panel-stack">
      <Row gutter={[16, 16]}>
        <Col xs={24} xl={12}>
          <Card title="Tutor" extra={<StatusTag value={tutorStatus(tutor.status)} />}>
            <Descriptions column={1} size="small">
              <Descriptions.Item label="Name">{tutor.name}</Descriptions.Item>
              <Descriptions.Item label="Description">{tutor.description || "No description provided."}</Descriptions.Item>
              <Descriptions.Item label="Created">{formatDateTime(tutor.createdUtc)}</Descriptions.Item>
              <Descriptions.Item label="Modified">{formatDateTime(tutor.modifiedUtc)}</Descriptions.Item>
            </Descriptions>
          </Card>
        </Col>
        <Col xs={24} xl={12}>
          <Card title="Current runtime configuration">
            {activeRevision ? (
              <Descriptions column={1} size="small">
                <Descriptions.Item label="Active revision">Revision {activeRevision.versionNumber}</Descriptions.Item>
                <Descriptions.Item label="Prompt">{activeRevision.promptVersionName ? `v${activeRevision.promptVersionNumber} · ${activeRevision.promptVersionName}` : "—"}</Descriptions.Item>
                <Descriptions.Item label="Interaction model">{activeRevision.interactionModelName || "—"}</Descriptions.Item>
                <Descriptions.Item label="Provider model">{activeRevision.providerModelName || "—"}</Descriptions.Item>
                <Descriptions.Item label="Activation slug"><Typography.Text code copyable>{activeRevision.activationSlug || "—"}</Typography.Text></Descriptions.Item>
                <Descriptions.Item label="Snapshot"><Tag color={activeRevision.latestSnapshotId ? "success" : "warning"}>{activeRevision.latestSnapshotId ? "Captured" : "Missing"}</Tag></Descriptions.Item>
              </Descriptions>
            ) : <Typography.Text type="secondary">No revision is active yet.</Typography.Text>}
          </Card>
        </Col>
      </Row>

      <Card title="Setup checklist">
        {!ready && runtimeReadiness ? <Alert
          type="warning"
          showIcon
          title={readinessReason?.code === "KnowledgeNotVectorized" ? "Knowledge is not ready" : readinessReason?.code?.includes("InteractionModel") || readinessReason?.code === "ProviderDisabled" ? "Interaction model unavailable" : "Chat configuration incomplete"}
          description={<>
            <div>{readinessReason?.message || "Complete the active revision's runtime configuration."}</div>
            {canManage ? <Button size="small" style={{ marginTop: 8 }} onClick={() => onOpenTab(readinessReason?.code === "KnowledgeNotVectorized" || readinessReason?.code?.startsWith("KnowledgeEmbedding") ? "knowledge" : "revisions")}>{readinessReason?.code === "KnowledgeNotVectorized" || readinessReason?.code?.startsWith("KnowledgeEmbedding") ? "Open knowledge" : "Open revisions"}</Button> : null}
          </>}
          style={{ marginBottom: 16 }}
        /> : null}
        {!runtimeReadiness ? <Typography.Text type="secondary">Runtime readiness could not be loaded.</Typography.Text> : null}
        <div className="tutor-resource-list tutor-checklist">
          {checklist.map((item) => (
            <Flex className="tutor-resource-row" key={item.label} align="center" gap={12}>
              {item.complete ? <CheckCircleFilled className="tutor-check-complete" /> : <ClockCircleOutlined className="tutor-check-pending" />}
              <div className="tutor-resource-copy"><Typography.Text strong>{item.label}</Typography.Text><Typography.Text type="secondary">{item.complete ? "Complete" : "Action required"}</Typography.Text></div>
              {!item.complete && canManage && item.action ? <Button size="small" onClick={() => item.tab && onOpenTab(item.tab)}>{item.action}</Button> : null}
              {item.label === "Ready for chat" && ready ? <>
                <Typography.Text type="secondary">Active revision has a valid runtime configuration.</Typography.Text>
                {activeRevision ? <Button type="primary" size="small" icon={<ExperimentOutlined />} onClick={() => navigate(`/benchmarking?tab=new&tutorId=${tutor.id}&revisionId=${activeRevision.id}`)}>Benchmark</Button> : null}
              </> : null}
            </Flex>
          ))}
        </div>
      </Card>
    </div>
  );
}
