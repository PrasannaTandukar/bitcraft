import { SettingOutlined } from "@ant-design/icons";
import { Button, Card, Descriptions, Flex, Space, Tag, Typography } from "antd";

export default function TutorRuntimeHeader({ tutor, canManage, onConfigure, onBenchmark }) {
  if (!tutor) return null;
  return (
    <Card className="runtime-header-card">
      <Flex justify="space-between" align="flex-start" gap={16} wrap>
        <div className="runtime-header-copy">
          <Space size={8} wrap>
            <Typography.Title level={4}>{tutor.tutorName}</Typography.Title>
            <Tag color="success">Active · Revision {tutor.revisionNumber}</Tag>
          </Space>
          {tutor.description ? <Typography.Paragraph type="secondary">{tutor.description}</Typography.Paragraph> : null}
        </div>
        {canManage ? <Space wrap><Button icon={<SettingOutlined />} onClick={onConfigure}>Configure</Button><Button type="primary" onClick={onBenchmark}>Benchmark</Button></Space> : null}
      </Flex>
      <Descriptions size="small" column={{ xs: 1, sm: 3 }}>
        <Descriptions.Item label="Interaction model">{tutor.model.name}</Descriptions.Item>
        <Descriptions.Item label="Query embedding">{tutor.embeddingModel?.name || "Not required"}</Descriptions.Item>
        <Descriptions.Item label="Tutor knowledge">{tutor.knowledgeDocumentCount} document{tutor.knowledgeDocumentCount === 1 ? "" : "s"}</Descriptions.Item>
      </Descriptions>
    </Card>
  );
}
