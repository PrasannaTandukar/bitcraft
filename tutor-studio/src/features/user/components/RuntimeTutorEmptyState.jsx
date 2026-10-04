import { MessageOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Empty, Typography } from "antd";

const knowledgeReason = (code) => code === "KnowledgeNotVectorized" || code?.startsWith("KnowledgeEmbedding");

export default function RuntimeTutorEmptyState({ readiness = [], canManage, onConfigure }) {
  return (
    <Empty image={<MessageOutlined className="runtime-empty-icon" />} description="No tutors are ready for chat">
      {readiness.length ? <div className="runtime-readiness-list">
        {readiness.map((item) => (
          <Card key={item.tutorId} size="small" title={item.tutorName} style={{ textAlign: "left", marginBottom: 12 }}>
            <Alert type="warning" showIcon message={item.reasons?.[0]?.message || "This tutor needs configuration before it can be used in chat."} />
            {canManage ? <Button size="small" style={{ marginTop: 8 }} onClick={() => onConfigure(item.tutorId, knowledgeReason(item.reasons?.[0]?.code) ? "knowledge" : "revisions")}>Configure tutor</Button> : null}
          </Card>
        ))}
      </div> : <Typography.Paragraph type="secondary">Create and activate a tutor revision to make it available here.</Typography.Paragraph>}
      {canManage && !readiness.length ? <Button type="primary" onClick={() => onConfigure(null, null)}>Configure tutors</Button> : null}
    </Empty>
  );
}
