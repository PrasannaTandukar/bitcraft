import { DeleteOutlined, EditOutlined, EyeOutlined, ThunderboltOutlined } from "@ant-design/icons";
import { Button, Flex, Space, Tag, Tooltip, Typography } from "antd";

function fileIcon(mimeType, name) {
  if (mimeType === "application/pdf" || name?.toLowerCase().endsWith(".pdf")) return "PDF";
  if (mimeType?.includes("wordprocessingml") || name?.toLowerCase().endsWith(".docx")) return "DOC";
  return "TXT";
}

function formatDate(value) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "Unknown date" : date.toLocaleDateString();
}

function statusLabel(document, status) {
  if (!document.hasRawText) return { color: "warning", text: "No extracted text" };
  if (status?.unavailable) return { color: "default", text: "Status unavailable" };
  if (status?.isStale) return { color: "warning", text: "Needs re-vectorization" };
  if (status?.isVectorized) return { color: "success", text: `Vectorized · ${status.chunkCount} chunks` };
  if (status === undefined) return { color: "processing", text: "Checking vectorization" };
  return { color: "blue", text: "Ready for vectorization" };
}

export default function DocumentRow({ document, folderName, status, canManage, onPreview, onReplace, onVectorize, onDelete, onMove }) {
  const state = statusLabel(document, status);
  const actions = [
    <Tooltip key="preview" title="Preview extracted text"><Button aria-label="Preview extracted text" icon={<EyeOutlined />} onClick={() => onPreview(document)} /></Tooltip>,
  ];
  if (canManage && !document.hasRawText) actions.push(<Tooltip key="replace" title="Add file content"><Button aria-label="Add file content" icon={<EditOutlined />} onClick={() => onReplace(document)} /></Tooltip>);
  if (canManage && document.hasRawText) actions.push(<Tooltip key="vectorize" title={status?.isVectorized ? "Re-vectorize" : "Vectorize"}><Button aria-label={status?.isVectorized ? "Re-vectorize" : "Vectorize"} icon={<ThunderboltOutlined />} onClick={() => onVectorize(document)} /></Tooltip>);
  if (canManage) actions.push(<Tooltip key="delete" title="Delete file"><Button aria-label="Delete file" danger icon={<DeleteOutlined />} onClick={() => onDelete(document)} /></Tooltip>);

  return (
    <div className="document-list-item">
      <span className="document-type-icon">{fileIcon(document.mimeType, document.name)}</span>
      <div className="document-list-copy">
        <Typography.Text strong>{document.name || "Unnamed document"}</Typography.Text>
        <Space orientation="vertical" size={5}>
          <Typography.Text type="secondary">{document.mimeType || "Unknown type"} · {formatDate(document.lastModifiedUtc || document.createdUtc)}{folderName ? ` · ${folderName}` : ""}</Typography.Text>
          <Flex gap={6} wrap>
            <Tag color={document.hasRawText ? "success" : "warning"}>{document.hasRawText ? `${Number(document.rawTextLength || 0).toLocaleString()} extracted characters` : "No extracted text"}</Tag>
            <Tag color={state.color}>{state.text}</Tag>
          </Flex>
        </Space>
      </div>
      <Flex className="document-row-actions" gap={8}>{actions}</Flex>
    </div>
  );
}
