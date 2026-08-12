import { CopyOutlined, DeleteOutlined, DownOutlined, UpOutlined } from "@ant-design/icons";
import { Button, Card, Col, Flex, Form, Input, Row, Tooltip, Typography } from "antd";

export default function BenchmarkCaseEditor({ runCase, index, total, automatic, onChange, onRemove, onDuplicate, onMove }) {
  const set = (field, value) => onChange(index, { ...runCase, [field]: value });

  return (
    <Card
      size="small"
      className="benchmark-case-card"
      title={<Typography.Text strong>Case {index + 1}</Typography.Text>}
      extra={(
        <Flex gap={4}>
          <Tooltip title="Move up"><Button type="text" size="small" aria-label="Move case up" icon={<UpOutlined />} onClick={() => onMove(index, -1)} disabled={index === 0} /></Tooltip>
          <Tooltip title="Move down"><Button type="text" size="small" aria-label="Move case down" icon={<DownOutlined />} onClick={() => onMove(index, 1)} disabled={index === total - 1} /></Tooltip>
          <Tooltip title="Duplicate"><Button type="text" size="small" aria-label="Duplicate case" icon={<CopyOutlined />} onClick={() => onDuplicate(index)} /></Tooltip>
          <Tooltip title="Remove"><Button type="text" size="small" danger aria-label="Remove case" icon={<DeleteOutlined />} onClick={() => onRemove(index)} disabled={total === 1} /></Tooltip>
        </Flex>
      )}
    >
      <Form layout="vertical" requiredMark={false}>
        <Form.Item label="Question" required>
          <Input.TextArea autoSize={{ minRows: 2, maxRows: 6 }} value={runCase.inputText} onChange={(event) => set("inputText", event.target.value)} />
        </Form.Item>
        {!automatic ? (
          <Form.Item label="Actual answer" required>
            <Input.TextArea autoSize={{ minRows: 3, maxRows: 10 }} value={runCase.actualOutput} onChange={(event) => set("actualOutput", event.target.value)} />
          </Form.Item>
        ) : null}
        <Row gutter={16}>
          <Col xs={24} lg={12}><Form.Item label="Expected answer"><Input.TextArea autoSize={{ minRows: 3, maxRows: 10 }} value={runCase.expectedOutput} onChange={(event) => set("expectedOutput", event.target.value)} /></Form.Item></Col>
          <Col xs={24} lg={12}><Form.Item label="Context override"><Input.TextArea autoSize={{ minRows: 3, maxRows: 10 }} value={runCase.contextText} onChange={(event) => set("contextText", event.target.value)} /></Form.Item></Col>
          <Col span={24}><Form.Item label="Metadata (JSON)"><Input.TextArea className="workspace-code" autoSize={{ minRows: 2, maxRows: 8 }} value={runCase.metadataJson} onChange={(event) => set("metadataJson", event.target.value)} placeholder='{"difficulty":"easy"}' /></Form.Item></Col>
        </Row>
      </Form>
    </Card>
  );
}
