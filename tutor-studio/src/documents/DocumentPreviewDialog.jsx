import { Alert, Button, Descriptions, Drawer, Empty, Space, Spin, Typography } from "antd";
import { useEffect, useState } from "react";
import { getApiErrorDescription } from "../api/client.js";
import { getDocument } from "./documentApi.js";

function safeLength(value) {
  return typeof value === "string" ? value.length : 0;
}

export default function DocumentPreviewDialog({ document, canManage, onClose, onReplace }) {
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    getDocument(document.id, controller.signal)
      .then((value) => {
        if (value && typeof value === "object") setDetail(value);
        else setError("The document response was invalid.");
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "The extracted text could not be loaded."));
      });
    return () => controller.abort();
  }, [document.id]);

  return (
    <Drawer
      title="Extracted text preview"
      open
      onClose={onClose}
      size={760}
      footer={(
        <Space style={{ width: "100%", justifyContent: "flex-end" }}>
          {canManage && detail ? <Button onClick={() => onReplace(document)}>{safeLength(detail.rawText) > 0 ? "Replace file content" : "Add file content"}</Button> : null}
          <Button type="primary" onClick={onClose}>Close</Button>
        </Space>
      )}
    >
      {!detail && !error ? <div className="workspace-loading"><Spin description="Loading extracted text" /></div> : null}
      {error ? <Alert type="error" title={error} showIcon /> : null}
      {detail ? (
        <Space orientation="vertical" size={20} style={{ width: "100%" }}>
          <Descriptions column={1} size="small">
            <Descriptions.Item label="Filename">{detail.name || "Unnamed document"}</Descriptions.Item>
            <Descriptions.Item label="Type">{detail.mimeType || "Unknown"}</Descriptions.Item>
            <Descriptions.Item label="Characters">{safeLength(detail.rawText).toLocaleString()}</Descriptions.Item>
          </Descriptions>
          {safeLength(detail.rawText) > 0 ? <pre className="document-text-preview">{detail.rawText}</pre> : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No extracted text is stored for this document" />}
        </Space>
      ) : null}
    </Drawer>
  );
}
